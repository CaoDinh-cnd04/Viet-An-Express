using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Notifications;

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

    /// <summary>Thông báo popup CRM soạn (TConfig ThongBao_NoiDung + ThongBao_Show) → <c>{ show, html }</c>.</summary>
    [HttpGet("popup")]
    [HasPermission(IdentityPermissions.Notifications)]
    [ProducesResponseType<PopupNoticeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Popup(CancellationToken ct)
    {
        var result = await Sender.Send(new GetPopupNoticeQuery(), ct);
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

/// <summary>Ảnh của tin trong thông báo — công khai để khách và email (Gmail tải ảnh qua proxy) xem được.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/public/notifications")]
[Tags("Công khai")]
[AllowAnonymous]
internal sealed class PublicNotificationsController : ApiControllerBase
{
    [HttpGet("images/{name}")]
    public IActionResult Image(string name, [FromServices] NotificationImageStore images)
    {
        if (images.Find(name) is not { } image) return NotFound();
        // Tên tệp = mã băm nội dung → không bao giờ đổi.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return PhysicalFile(image.Path, image.ContentType);
    }
}
