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

/// <summary>Trang Trợ giúp: gửi yêu cầu hỗ trợ — email về hộp thư nhận của Việt An (không lưu database).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/support")]
[Tags("Trợ giúp")]
internal sealed class SupportController : ApiControllerBase
{
    private const long MaxRequestBytes = SupportRules.AttachmentMaxBytes + 1024 * 1024;

    /// <summary>multipart/form-data: <c>category</c>, <c>subject</c>, <c>message</c>, <c>contact</c>, <c>attachment?</c> (≤ 10 MB: ảnh, PDF, Excel, CSV, Word).</summary>
    [HttpPost("feedback")]
    [HasPermission(IdentityPermissions.Feedback)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(
        [FromForm] string? category, [FromForm] string? subject, [FromForm] string? message, [FromForm] string? contact, CancellationToken ct)
    {
        SupportAttachment? attachment = null;
        if (Request.Form.Files.GetFile("attachment") is { } file)
        {
            if (file.Length > SupportRules.AttachmentMaxBytes) return Problem(SupportErrors.AttachmentTooLarge);
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer, ct);
            attachment = new SupportAttachment(FeedbackRules.SafeFileName(file.FileName),
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, buffer.ToArray());
        }
        var result = await Sender.Send(new SendSupportRequestCommand(category, subject, message, contact, attachment), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã gửi yêu cầu — Việt An sẽ liên hệ lại sớm")) : Problem(result.Error);
    }
}
