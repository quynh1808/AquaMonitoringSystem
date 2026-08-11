using System.Security.Claims;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AquacultureMonitoring.Services;

public sealed record LoginResult(bool Success, AppUser? User, string Error);

public sealed class UserAccountService
{
    private readonly MongoContext _db;
    private readonly PasswordHasher _hasher;
    private readonly AuthenticationOptions _options;

    public UserAccountService(MongoContext db, PasswordHasher hasher, IOptions<AuthenticationOptions> options)
    { _db = db; _hasher = hasher; _options = options.Value; }

    public async Task EnsureBootstrapAdminAsync(CancellationToken ct)
    {
        if (await _db.Users.CountDocumentsAsync(FilterDefinition<AppUser>.Empty, cancellationToken: ct) > 0) return;
        await CreateAsync(_options.BootstrapAdminUsername, _options.BootstrapAdminDisplayName,
            _options.BootstrapAdminPassword, [SystemRoles.Administrator], [], ct);
    }

    public async Task<LoginResult> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        var normalized = Normalize(username);
        var user = await _db.Users.Find(x => x.NormalizedUsername == normalized).FirstOrDefaultAsync(ct);
        if (user is null || !user.IsEnabled) return new(false, null, "Tài khoản hoặc mật khẩu không đúng.");
        if (user.LockedUntilUtc > DateTime.UtcNow) return new(false, null, "Tài khoản đang tạm khóa do đăng nhập sai nhiều lần.");
        if (!_hasher.Verify(password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _options.MaxFailedLoginAttempts)
            { user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(_options.LockoutMinutes); user.FailedLoginCount = 0; }
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
            return new(false, null, "Tài khoản hoặc mật khẩu không đúng.");
        }
        user.FailedLoginCount = 0; user.LockedUntilUtc = null; user.LastLoginAtUtc = DateTime.UtcNow; user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return new(true, user, string.Empty);
    }

    public ClaimsPrincipal CreatePrincipal(AppUser user)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.Username), new("display_name", user.DisplayName) };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.AllowedPondIds.Select(id => new Claim("pond_access", id)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "AquaOpsCookie"));
    }

    public Task<List<AppUser>> GetUsersAsync(CancellationToken ct) =>
        _db.Users.Find(FilterDefinition<AppUser>.Empty).SortBy(x => x.DisplayName).ToListAsync(ct);

    public async Task<AppUser> CreateAsync(string username, string displayName, string password, IEnumerable<string> roles, IEnumerable<string> pondIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Tên đăng nhập và tên hiển thị không được để trống.");
        EnsureStrongEnough(password);
        var normalized = Normalize(username);
        if (await _db.Users.CountDocumentsAsync(x => x.NormalizedUsername == normalized, cancellationToken: ct) > 0) throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");
        var roleList = roles.Where(SystemRoles.All.Contains).Distinct().ToList();
        if (roleList.Count == 0) roleList.Add(SystemRoles.Viewer);
        var passwordData = _hasher.Hash(password, _options.PasswordIterations);
        var user = new AppUser { Username = username.Trim(), NormalizedUsername = normalized, DisplayName = displayName.Trim(), PasswordHash = passwordData.Hash, PasswordSalt = passwordData.Salt, PasswordIterations = passwordData.Iterations, PasswordChangedAtUtc = DateTime.UtcNow, Roles = roleList, AllowedPondIds = pondIds.Distinct().ToList() };
        await _db.Users.InsertOneAsync(user, cancellationToken: ct); return user;
    }

    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken ct)
    {
        var target = await _db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        if (!enabled && target.IsEnabled && target.Roles.Contains(SystemRoles.Administrator))
        {
            var enabledAdmins = await _db.Users.CountDocumentsAsync(
                x => x.IsEnabled && x.Roles.Contains(SystemRoles.Administrator), cancellationToken: ct);
            if (enabledAdmins <= 1) throw new InvalidOperationException("Không thể khóa Administrator hoạt động cuối cùng.");
        }
        await _db.Users.UpdateOneAsync(x => x.Id == id,
            Builders<AppUser>.Update.Set(x => x.IsEnabled, enabled).Set(x => x.UpdatedAtUtc, DateTime.UtcNow),
            cancellationToken: ct);
    }

    public async Task ResetPasswordAsync(string id, string password, CancellationToken ct)
    {
        EnsureStrongEnough(password);
        var value = _hasher.Hash(password, _options.PasswordIterations);
        var update = PasswordUpdate(value);
        var result = await _db.Users.UpdateOneAsync(x => x.Id == id, update, cancellationToken: ct);
        if (result.MatchedCount == 0) throw new InvalidOperationException("Không tìm thấy tài khoản.");
    }

    /// <summary>Changes the current user's password only after verifying the existing secret.</summary>
    public async Task ChangePasswordAsync(string id, string currentPassword, string newPassword, CancellationToken ct)
    {
        EnsureStrongEnough(newPassword);
        var user = await _db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        if (!user.IsEnabled) throw new InvalidOperationException("Tài khoản đã bị vô hiệu hóa.");
        if (!_hasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            throw new InvalidOperationException("Mật khẩu hiện tại không đúng.");
        if (_hasher.Verify(newPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            throw new InvalidOperationException("Mật khẩu mới phải khác mật khẩu hiện tại.");

        var value = _hasher.Hash(newPassword, _options.PasswordIterations);
        // Include the verified hash in the filter so a concurrent admin reset cannot be
        // overwritten by a change based on stale credentials.
        var result = await _db.Users.UpdateOneAsync(
            x => x.Id == id && x.PasswordHash == user.PasswordHash && x.PasswordSalt == user.PasswordSalt,
            PasswordUpdate(value),
            cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException("Mật khẩu vừa được thay đổi ở phiên khác. Vui lòng thử lại.");
    }

    /// <summary>
    /// Recovery operation used only by the local container command. It refuses non-admin users,
    /// re-enables the account and clears lockout without printing or returning the password.
    /// </summary>
    public async Task ResetAdministratorPasswordAsync(string username, string password, CancellationToken ct)
    {
        EnsureStrongEnough(password);
        var normalized = Normalize(username);
        var user = await _db.Users.Find(x => x.NormalizedUsername == normalized).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản Administrator.");
        if (!user.Roles.Contains(SystemRoles.Administrator))
            throw new InvalidOperationException("Lệnh recovery chỉ được phép đặt lại tài khoản Administrator.");

        var value = _hasher.Hash(password, _options.PasswordIterations);
        var update = PasswordUpdate(value).Set(x => x.IsEnabled, true);
        var result = await _db.Users.UpdateOneAsync(
            x => x.Id == user.Id && x.Roles.Contains(SystemRoles.Administrator),
            update,
            cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException("Tài khoản không còn quyền Administrator.");
    }

    public async Task UpdateAccessAsync(string id, IEnumerable<string> roles, IEnumerable<string> pondIds, CancellationToken ct)
    {
        var validRoles = roles.Where(SystemRoles.All.Contains).Distinct().ToList();
        if (validRoles.Count == 0) validRoles.Add(SystemRoles.Viewer);
        var target = await _db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        if (target.IsEnabled && target.Roles.Contains(SystemRoles.Administrator) && !validRoles.Contains(SystemRoles.Administrator))
        {
            var enabledAdmins = await _db.Users.CountDocumentsAsync(
                x => x.IsEnabled && x.Roles.Contains(SystemRoles.Administrator), cancellationToken: ct);
            if (enabledAdmins <= 1) throw new InvalidOperationException("Không thể hạ quyền Administrator hoạt động cuối cùng.");
        }
        await _db.Users.UpdateOneAsync(x => x.Id == id, Builders<AppUser>.Update.Set(x => x.Roles, validRoles).Set(x => x.AllowedPondIds, pondIds.Distinct().ToList()).Set(x => x.UpdatedAtUtc, DateTime.UtcNow), cancellationToken: ct);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static UpdateDefinition<AppUser> PasswordUpdate((string Hash, string Salt, int Iterations) value) =>
        Builders<AppUser>.Update
            .Set(x => x.PasswordHash, value.Hash)
            .Set(x => x.PasswordSalt, value.Salt)
            .Set(x => x.PasswordIterations, value.Iterations)
            .Set(x => x.FailedLoginCount, 0)
            .Set(x => x.LockedUntilUtc, null)
            .Set(x => x.PasswordChangedAtUtc, DateTime.UtcNow)
            .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);

    private static void EnsureStrongEnough(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Mật khẩu phải có ít nhất 12 ký tự.");
    }
}
