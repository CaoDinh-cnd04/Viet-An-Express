using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Admin;

/// <summary>Trang quản trị Việt An (/admin): đăng nhập tài khoản cố định. Thông báo: AdminNotificationsController; hỗ trợ &amp; góp ý: AdminSupportRequestsController.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin")]
[Tags("Quản trị")]
internal sealed class AdminController : ApiControllerBase
{
    /// <summary>Đăng nhập quản trị → token (gửi lại bằng header Authorization: Bearer).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<AdminSessionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(AdminLoginRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new AdminLoginCommand(body.UserName, body.Password), ct));

    /// <summary>Đăng xuất quản trị: thu hồi token đang dùng (header Authorization) — không đụng cookie phiên của khách.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await Sender.Send(new LogoutCommand(null, User.FindFirst(VaClaimTypes.TokenId)?.Value, AuthController.AccessTokenExpiry(User)), ct);
        return Ok(new ApiMessage("Đã đăng xuất"));
    }
}

internal sealed record AdminLoginRequest(string? UserName, string? Password);
