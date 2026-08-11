using System.Security.Claims;
using AquacultureMonitoring.Models;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Central authorization guard for farm and pond resources. UI authorization remains useful
/// for presentation, but every state-changing service method calls this guard as the final
/// server-side enforcement point.
/// </summary>
public sealed class FarmAccessService
{
    public bool CanAccessPond(ClaimsPrincipal user, string pondId)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole(SystemRoles.Administrator)) return true;

        var assignedPonds = user.FindAll("pond_access").Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return assignedPonds.Count == 0 || assignedPonds.Contains(pondId);
    }

    public void EnsureCanViewPond(ClaimsPrincipal user, string pondId)
    {
        if (!CanAccessPond(user, pondId))
            throw new UnauthorizedAccessException("Tài khoản không có quyền truy cập ao nuôi này.");
    }

    public void EnsureCanManageFarm(ClaimsPrincipal user)
    {
        EnsureAuthenticated(user);
        if (!user.IsInRole(SystemRoles.Administrator) && !user.IsInRole(SystemRoles.FarmManager))
            throw new UnauthorizedAccessException("Thao tác yêu cầu quyền Administrator hoặc FarmManager.");
    }

    public void EnsureCanOperatePond(ClaimsPrincipal user, string pondId)
    {
        EnsureAuthenticated(user);
        if (!user.IsInRole(SystemRoles.Administrator) &&
            !user.IsInRole(SystemRoles.FarmManager) &&
            !user.IsInRole(SystemRoles.Technician))
            throw new UnauthorizedAccessException("Tài khoản chỉ có quyền xem dữ liệu.");
        EnsureCanViewPond(user, pondId);
    }

    public static string ActorName(ClaimsPrincipal user) =>
        user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    public static string DisplayName(ClaimsPrincipal user) =>
        user.FindFirstValue("display_name") ?? user.Identity?.Name ?? "Người dùng";

    private static void EnsureAuthenticated(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Phiên đăng nhập không còn hợp lệ.");
    }
}

