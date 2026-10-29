using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Trang quản trị Việt An: soạn, đăng, gỡ thông báo gửi khách (dbo.TThongBao).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin")]
[Tags("Quản trị")]
internal sealed class AdminNotificationsController : ApiControllerBase
{
    /// <summary>Thông báo quản trị đăng (200 mới nhất): trạng thái, các tin, đối tượng nhận, số khách đã đọc, kết quả gửi email.</summary>
    [HttpGet("notifications")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminNotificationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Notifications(CancellationToken ct) => OkData(await Sender.Send(new GetAdminNotificationsQuery(), ct));

    /// <summary>Số khách có tài khoản portal, chia theo loại khách — để chọn đối tượng nhận.</summary>
    [HttpGet("notifications/audience")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<NotificationAudienceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> NotificationAudience(CancellationToken ct) => OkData(await Sender.Send(new GetNotificationAudienceQuery(), ct));

    /// <summary>Khách đã đọc thông báo (500 lượt mới nhất).</summary>
    [HttpGet("notifications/{id:long}/readers")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<NotificationReaderDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> NotificationReaders(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetNotificationReadersQuery(id), ct));

    /// <summary>Tạo thông báo: <c>publish</c> false = lưu nháp; true = đăng (hẹn giờ nếu <c>publishAt</c> ở tương lai), <c>sendEmail</c> gửi email ở nền.</summary>
    [HttpPost("notifications")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<AdminNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateNotification(SaveNotificationRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(body.ToCommand(null), ct), body.Message);

    /// <summary>Sửa thông báo (nháp hoặc đã đăng — giữ lượt đã đọc; email đã gửi không gửi lại).</summary>
    [HttpPut("notifications/{id:long}")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<AdminNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotification(long id, SaveNotificationRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(body.ToCommand(id), ct), body.Publish ? "Đã lưu và đăng thông báo" : "Đã lưu nháp");

    /// <summary>Gửi thử email của nội dung đang soạn tới <c>to</c> (≤ 5 địa chỉ, cách nhau dấu phẩy) — không lưu, không gửi khách.</summary>
    [HttpPost("notifications/test-email")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestNotificationEmail(TestNotificationEmailRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new TestNotificationEmailCommand(body.To, body.Title, body.Items, body.Important), ct), "Đã gửi email thử");

    /// <summary>Gỡ thông báo khỏi portal của khách (đăng lại bằng PUT với <c>publish: true</c>).</summary>
    [HttpPost("notifications/{id:long}/remove")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiResponse<AdminNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveNotification(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new RemoveNotificationCommand(id), ct), "Đã gỡ thông báo khỏi portal của khách");

    [HttpDelete("notifications/{id:long}")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteNotification(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new DeleteNotificationCommand(id), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã xóa thông báo")) : Problem(result.Error);
    }

    /// <summary><c>multipart/form-data</c> trường <c>file</c>: ảnh JPG / PNG / WebP ≤ 2 MB → <c>{ data: { name } }</c> (tên tệp gửi lại trong <c>items[].images</c>).</summary>
    [HttpPost("notifications/images")]
    [HasPermission(IdentityPermissions.AdminNotifications)]
    [RequestSizeLimit(NotificationImageStore.MaxImageBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<NotificationImageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadNotificationImage([FromServices] NotificationImageStore images, CancellationToken ct)
    {
        if (!Request.HasFormContentType || Request.Form.Files.GetFile("file") is not { } upload) return Problem(ImageError("Chọn tệp ảnh"));
        if (upload.Length > NotificationImageStore.MaxImageBytes) return Problem(ImageError("Ảnh lớn hơn 2 MB"));
        using var buffer = new MemoryStream((int)upload.Length);
        await using (var stream = upload.OpenReadStream()) await stream.CopyToAsync(buffer, ct);
        try
        {
            var path = await images.SaveAsync(buffer.ToArray(), ct);
            return OkData(new NotificationImageDto(NotificationImageStore.NameOf(path)!));
        }
        catch (FormatException e)
        {
            return Problem(ImageError(e.Message));
        }
    }

    private static Error ImageError(string message) => Error.Validation("NOTIFICATION_IMAGE_INVALID", message);
}

internal sealed record NotificationImageDto(string Name);

internal sealed record TestNotificationEmailRequest(string? To, string? Title, IReadOnlyList<NotificationItemInput>? Items, bool Important);

internal sealed record SaveNotificationRequest(
    string? Title, IReadOnlyList<NotificationItemInput>? Items, bool Important, bool Pinned, bool SendEmail,
    string? Audience, IReadOnlyList<int>? CustomerTypes, IReadOnlyList<string>? CustomerCodes,
    DateTime? PublishAt, DateOnly? ExpiresOn, bool Publish)
{
    public SaveNotificationCommand ToCommand(long? id) =>
        new(id, Title, Items, Important, Pinned, SendEmail, Audience, CustomerTypes, CustomerCodes, PublishAt, ExpiresOn, Publish);

    public string Message => !Publish ? "Đã lưu nháp"
        : PublishAt is not null ? "Đã lưu — thông báo đăng theo giờ hẹn"
        : SendEmail ? "Đã đăng thông báo — đang gửi email cho khách" : "Đã đăng thông báo";
}
