using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Domain;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>Góp ý của khách (mục Tài khoản → Góp ý): gửi nội dung + tối đa 5 ảnh, xem góp ý đã gửi.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/account/feedback")]
[Tags("Tài khoản")]
internal sealed class FeedbackController : ApiControllerBase
{
    /// <summary>Tổng dung lượng 1 lần gửi: 5 ảnh × 5 MB + nội dung.</summary>
    private const long MaxRequestBytes = Feedback.MaxImages * (long)Feedback.ImageMaxBytes + 1024 * 1024;

    [HttpGet]
    [HasPermission(IdentityPermissions.Feedback)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FeedbackDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken ct) => FromResult(await Sender.Send(new GetMyFeedbackQuery(), ct));

    /// <summary>
    /// multipart/form-data: <c>message</c> (bắt buộc nếu không trả lời khảo sát), <c>contact</c> (không bắt buộc), <c>rating</c> (1–5 sao, không bắt buộc),
    /// <c>survey</c> (JSON <c>[{ id, question, answers, note }]</c>, không bắt buộc), <c>images</c> (0–5 ảnh JPG / PNG / WEBP / GIF, mỗi ảnh ≤ 5 MB).
    /// </summary>
    [HttpPost]
    [HasPermission(IdentityPermissions.Feedback)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<ApiResponse<FeedbackDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Send([FromForm] string? message, [FromForm] string? contact, [FromForm] int? rating, [FromForm] string? survey, CancellationToken ct)
    {
        var files = Request.Form.Files.GetFiles("images");
        if (files.Count > Feedback.MaxImages) return Problem(FeedbackErrors.TooManyImages);
        if (files.Any(f => f.Length > Feedback.ImageMaxBytes)) return Problem(FeedbackErrors.ImageTooLarge);

        var uploads = new List<FeedbackUpload>(files.Count);
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer, ct);
            uploads.Add(new FeedbackUpload(file.FileName, file.ContentType ?? "", buffer.ToArray()));
        }
        return FromResult(await Sender.Send(new SendFeedbackCommand(message, contact, rating, uploads, survey), ct), "Đã gửi góp ý — cảm ơn bạn!");
    }

    /// <summary>Ẩn góp ý khỏi trang khách; admin vẫn xem được nội dung và ảnh.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(IdentityPermissions.Feedback)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new DeleteFeedbackCommand(id), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã xóa góp ý")) : Problem(result.Error);
    }

    /// <summary>Ảnh của góp ý (khách: ảnh góp ý của mình; quản trị: mọi ảnh).</summary>
    [HttpGet("images/{id:long}")]
    [Authorize]
    public async Task<IActionResult> Image(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new GetFeedbackImageQuery(id), ct);
        if (result.IsFailure) return Problem(result.Error);
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(result.Value.Data, result.Value.ContentType);
    }
}

/// <summary>Trang quản trị Việt An (/admin): đăng nhập tài khoản cố định, xem góp ý của mọi khách, soạn thông báo gửi khách, xử lý sự cố.</summary>
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

    [HttpGet("feedback")]
    [HasPermission(IdentityPermissions.AdminFeedback)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminFeedbackDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Feedback(CancellationToken ct) => OkData(await Sender.Send(new GetAdminFeedbackQuery(), ct));

    /// <summary>Chi tiết 1 góp ý (kèm liên hệ của khách) — mở là tự đánh dấu đã xem.</summary>
    [HttpGet("feedback/{id:long}")]
    [HasPermission(IdentityPermissions.AdminFeedback)]
    [ProducesResponseType<ApiResponse<AdminFeedbackDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) => FromResult(await Sender.Send(new GetAdminFeedbackDetailQuery(id), ct));

    [HttpPost("feedback/{id:long}/seen")]
    [HasPermission(IdentityPermissions.AdminFeedback)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Seen(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new MarkFeedbackSeenCommand(id), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã đánh dấu đã xem")) : Problem(result.Error);
    }

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

    /// <summary>Sự cố của mọi khách (300 cập nhật gần nhất) kèm lịch sử trao đổi.</summary>
    [HttpGet("troubles")]
    [HasPermission(IdentityPermissions.AdminTroubles)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminTroubleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Troubles(CancellationToken ct) => OkData(await Sender.Send(new GetAdminTroublesQuery(), ct));

    /// <summary><c>{ reply?, status? }</c> — trả lời và / hoặc đổi trạng thái; khách nhận thông báo riêng trên portal.</summary>
    [HttpPost("troubles/{id}/reply")]
    [HasPermission(IdentityPermissions.AdminTroubles)]
    [ProducesResponseType<ApiResponse<AdminTroubleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplyTrouble(string id, TroubleReplyRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new AdminReplyTroubleCommand(id, body.Reply, body.Status), ct), "Đã cập nhật sự cố — khách đã nhận thông báo");

    /// <summary>Xóa hẳn góp ý và ảnh đính kèm (khách cũng không còn thấy).</summary>
    [HttpDelete("feedback/{id:long}")]
    [HasPermission(IdentityPermissions.AdminFeedback)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new AdminDeleteFeedbackCommand(id), ct);
        return result.IsSuccess ? Ok(new ApiMessage("Đã xóa góp ý")) : Problem(result.Error);
    }
}

internal sealed record AdminLoginRequest(string? UserName, string? Password);

internal sealed record CreateNotificationRequest(string? Title, string? Body, bool Important, bool SendEmail, string? Link = null);
