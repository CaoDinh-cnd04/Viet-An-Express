using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>Trang quản trị Việt An (/admin): đăng nhập tài khoản cố định, xử lý yêu cầu hỗ trợ &amp; sự cố, soạn thông báo gửi khách.</summary>
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

    /// <summary>Thông báo đã gửi (200 mới nhất) kèm kết quả gửi email và số tài khoản đã đọc.</summary>
    [HttpGet("notifications")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminNotificationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Notifications(CancellationToken ct) => OkData(await Sender.Send(new GetAdminNotificationsQuery(), ct));

    /// <summary>Gửi thông báo cho mọi khách; <c>sendEmail</c> → gửi thêm email ở nền (tối đa 450 người nhận / lần).</summary>
    [HttpPost("notifications")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<AdminNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateNotification(CreateNotificationRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new CreateNotificationCommand(body.Title, body.Body, body.Important, body.SendEmail, body.Link), ct),
            body.SendEmail ? "Đã đăng thông báo — đang gửi email cho khách" : "Đã đăng thông báo");

    [HttpDelete("notifications/{id:long}")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteNotification(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new DeleteNotificationCommand(id), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã xóa thông báo")) : Problem(result.Error);
    }

    /// <summary>Yêu cầu hỗ trợ &amp; sự cố của mọi khách (300 cập nhật gần nhất) kèm lịch sử trao đổi.</summary>
    [HttpGet("troubles")]
    [HasPermission(IdentityPermissions.AdminTroubles)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminTroubleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Troubles(CancellationToken ct) => OkData(await Sender.Send(new GetAdminTroublesQuery(), ct));

    /// <summary><c>{ reply?, status? }</c> — đổi trạng thái và / hoặc ghi chú cho khách (chỉ gửi trong thông báo riêng trên portal).</summary>
    [HttpPost("troubles/{id}/reply")]
    [HasPermission(IdentityPermissions.AdminTroubles)]
    [ProducesResponseType<ApiResponse<AdminTroubleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplyTrouble(string id, TroubleReplyRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new AdminReplyTroubleCommand(id, body.Reply, body.Status), ct), "Đã cập nhật yêu cầu — khách đã nhận thông báo");
}

internal sealed record AdminLoginRequest(string? UserName, string? Password);


internal sealed record CreateNotificationRequest(string? Title, string? Body, bool Important, bool SendEmail, string? Link = null);
