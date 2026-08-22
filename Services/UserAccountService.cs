using System.Security.Claims;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

public sealed record LoginResult(bool Success, AppUser? User, string Error);

/// <summary>Owns PostgreSQL-backed account, lockout and cookie-session version rules.</summary>
public sealed class UserAccountService(
    IDbContextFactory<AquaDbContext> factory,
    PasswordHasher hasher,
    IOptions<AuthenticationOptions> options)
{
    private readonly AuthenticationOptions _options = options.Value;

    public async Task EnsureBootstrapAdminAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Users.AnyAsync(ct)) return;
        await CreateAsync(_options.BootstrapAdminUsername, _options.BootstrapAdminDisplayName,
            _options.BootstrapAdminPassword, [SystemRoles.Administrator], [], ct);
    }

    public async Task<LoginResult> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 ||
            string.IsNullOrEmpty(password) || password.Length > 256)
            return new(false, null, "Tài khoản hoặc mật khẩu không đúng.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var normalized = Normalize(username);
        var user = await db.Users.FirstOrDefaultAsync(x => x.NormalizedUsername == normalized, ct);
        if (user is null || !user.IsEnabled) return new(false, null, "Tài khoản hoặc mật khẩu không đúng.");
        if (user.LockedUntilUtc > DateTime.UtcNow) return new(false, null, "Tài khoản đang tạm khóa do đăng nhập sai nhiều lần.");
        if (!hasher.Verify(password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _options.MaxFailedLoginAttempts)
            { user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(_options.LockoutMinutes); user.FailedLoginCount = 0; }
            user.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new(false, null, "Tài khoản hoặc mật khẩu không đúng.");
        }
        user.FailedLoginCount = 0; user.LockedUntilUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow; user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(true, user, string.Empty);
    }

    public ClaimsPrincipal CreatePrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.Username),
            new("display_name", user.DisplayName), new("session_version", user.SessionVersion.ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.AllowedPondIds.Select(id => new Claim("pond_access", id)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "AquaOpsCookie"));
    }

    public async Task<List<AppUser>> GetUsersAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(ct);
    }

    public async Task<bool> IsSessionValidAsync(string id, int sessionVersion, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var state = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.IsEnabled, x.SessionVersion }).FirstOrDefaultAsync(ct);
        return state is not null && state.IsEnabled && state.SessionVersion == sessionVersion;
    }

    public async Task<AppUser> CreateAsync(string username, string displayName, string password,
        IEnumerable<string> roles, IEnumerable<string> pondIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Tên đăng nhập và tên hiển thị không được để trống.");
        EnsureStrongEnough(password);
        var normalized = Normalize(username);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Users.AnyAsync(x => x.NormalizedUsername == normalized, ct))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");
        var roleList = roles.Where(SystemRoles.All.Contains).Distinct().ToList();
        if (roleList.Count == 0) roleList.Add(SystemRoles.Viewer);
        var passwordData = hasher.Hash(password, _options.PasswordIterations);
        var user = new AppUser
        {
            Username = username.Trim(), NormalizedUsername = normalized, DisplayName = displayName.Trim(),
            PasswordHash = passwordData.Hash, PasswordSalt = passwordData.Salt,
            PasswordIterations = passwordData.Iterations, PasswordChangedAtUtc = DateTime.UtcNow,
            Roles = roleList, AllowedPondIds = pondIds.Distinct().ToList()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var target = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        if (!enabled && target.IsEnabled && target.Roles.Contains(SystemRoles.Administrator))
            await EnsureAnotherAdministratorAsync(db, target.Id, ct);
        target.IsEnabled = enabled; target.SessionVersion++; target.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ResetPasswordAsync(string id, string password, CancellationToken ct)
    {
        EnsureStrongEnough(password);
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        ApplyPassword(user, hasher.Hash(password, _options.PasswordIterations));
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(string id, string currentPassword, string newPassword, CancellationToken ct)
    {
        EnsureStrongEnough(newPassword);
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        if (!user.IsEnabled) throw new InvalidOperationException("Tài khoản đã bị vô hiệu hóa.");
        if (!hasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            throw new InvalidOperationException("Mật khẩu hiện tại không đúng.");
        if (hasher.Verify(newPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            throw new InvalidOperationException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        ApplyPassword(user, hasher.Hash(newPassword, _options.PasswordIterations));
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetAdministratorPasswordAsync(string username, string password, CancellationToken ct)
    {
        EnsureStrongEnough(password);
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(x => x.NormalizedUsername == Normalize(username), ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản Administrator.");
        if (!user.Roles.Contains(SystemRoles.Administrator))
            throw new InvalidOperationException("Lệnh recovery chỉ được phép đặt lại tài khoản Administrator.");
        ApplyPassword(user, hasher.Hash(password, _options.PasswordIterations));
        user.IsEnabled = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAccessAsync(string id, IEnumerable<string> roles, IEnumerable<string> pondIds, CancellationToken ct)
    {
        var validRoles = roles.Where(SystemRoles.All.Contains).Distinct().ToList();
        if (validRoles.Count == 0) validRoles.Add(SystemRoles.Viewer);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var target = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        if (target.IsEnabled && target.Roles.Contains(SystemRoles.Administrator) &&
            !validRoles.Contains(SystemRoles.Administrator))
            await EnsureAnotherAdministratorAsync(db, target.Id, ct);
        target.Roles = validRoles; target.AllowedPondIds = pondIds.Distinct().ToList();
        target.SessionVersion++; target.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task EnsureAnotherAdministratorAsync(AquaDbContext db, string excludedId, CancellationToken ct)
    {
        // Roles are JSONB because they are small claim sets. Load enabled users and evaluate the
        // final administrator invariant in the same transaction.
        var candidates = await db.Users.AsNoTracking().Where(x => x.IsEnabled && x.Id != excludedId).ToListAsync(ct);
        if (!candidates.Any(x => x.Roles.Contains(SystemRoles.Administrator)))
            throw new InvalidOperationException("Không thể khóa hoặc hạ quyền Administrator hoạt động cuối cùng.");
    }

    private static void ApplyPassword(AppUser user, (string Hash, string Salt, int Iterations) value)
    {
        user.PasswordHash = value.Hash; user.PasswordSalt = value.Salt; user.PasswordIterations = value.Iterations;
        user.FailedLoginCount = 0; user.LockedUntilUtc = null; user.SessionVersion++;
        user.PasswordChangedAtUtc = DateTime.UtcNow; user.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static void EnsureStrongEnough(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Mật khẩu phải có ít nhất 12 ký tự.");
    }
}
