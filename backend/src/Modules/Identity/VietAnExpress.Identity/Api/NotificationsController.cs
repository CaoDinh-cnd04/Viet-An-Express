using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>Trung tâm thông báo của khách: thông báo quản trị Việt An gửi, đánh dấu đã đọc (riêng từng tài khoản chính / con).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/notifications")]
[Tags("Thông báo")]
internal sealed class NotificationsController : ApiControllerBase
{
    /// <summary>50 thông báo mới nhất → <c>{ data: [{ id, imp, unread, title, date, body }], unreadCount }</c>.</summary>
    [HttpGet]
    [HasPermission(IdentityPermissions.Notifications)]
    [ProducesResponseType<NotificationListDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await Sender.Send(new GetNotificationsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpPost("{id:long}/read")]
    [HasPermission(IdentityPermissions.Notifications)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Read(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new MarkNotificationReadCommand(id), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã đánh dấu đã đọc")) : Problem(result.Error);
    }

    [HttpPost("mark-all-read")]
    [HasPermission(IdentityPermissions.Notifications)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var result = await Sender.Send(new MarkAllNotificationsReadCommand(), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã đánh dấu đã đọc tất cả")) : Problem(result.Error);
    }
}
