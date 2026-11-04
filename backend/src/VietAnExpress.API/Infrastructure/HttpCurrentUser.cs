using System.Globalization;
using System.Security.Claims;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Authorization;

namespace VietAnExpress.API.Infrastructure;

/// <summary>Đọc người dùng hiện tại từ claim của JWT trong HttpContext.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string? UserName => Principal?.FindFirstValue(VaClaimTypes.Name);

    public long? CustomerId =>
        long.TryParse(Principal?.FindFirstValue(VaClaimTypes.CustomerId), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public long? StaffId =>
        long.TryParse(Principal?.FindFirstValue(VaClaimTypes.StaffId), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public int? VietAnUserId =>
        int.TryParse(Principal?.FindFirstValue(VaClaimTypes.VietAnUserId), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public bool HasPermission(string permission) => Principal?.HasClaim(VaClaimTypes.Permission, permission) == true;
}
