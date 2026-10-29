using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Hỗ trợ &amp; sự cố của khách (dbo.TContact): gửi sự cố vận đơn / yêu cầu hỗ trợ / góp ý, xem danh sách và trạng thái.
/// CS xử lý ở phần mềm cũ / trang /admin; trả lời qua lại nối vào TContact.Content (<see cref="SupportThread"/>).
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/troubles")]
[Tags("Hỗ trợ & sự cố")]
internal sealed class SupportRequestsController : ApiControllerBase
{
    private const long MaxRequestBytes = SupportRequest.FileMaxBytes + 1024 * 1024;

    [HttpGet]
    [HasPermission(IdentityPermissions.SupportRequests)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SupportRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => FromResult(await Sender.Send(new GetSupportRequestsQuery(), ct));

    /// <summary>
    /// multipart/form-data: <c>kind</c> (ho_tro = hỗ trợ &amp; sự cố / gop_y), <c>bill</c>, <c>cnee</c>, <c>ct</c>, <c>type</c>, <c>subject</c>, <c>lv</c>,
    /// <c>desc</c>, <c>req</c>, <c>contact</c>, <c>file?</c> (≤ 10 MB: ảnh, PDF, Excel, CSV, Word — chỉ gửi kèm email, không lưu database).
    /// </summary>
    [HttpPost]
    [HasPermission(IdentityPermissions.SupportRequests)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<ApiResponse<SupportRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromForm] NewSupportRequest body, CancellationToken ct)
    {
        SupportRequestFile? file = null;
        if (Request.Form.Files.GetFile("file") is { } upload)
        {
            if (upload.Length > SupportRequest.FileMaxBytes) return Problem(SupportRequestErrors.FileTooLarge);
            await using var stream = upload.OpenReadStream();
            using var buffer = new MemoryStream((int)upload.Length);
            await stream.CopyToAsync(buffer, ct);
            file = new SupportRequestFile(upload.FileName, string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType, buffer.ToArray());
        }
        var result = await Sender.Send(new CreateSupportRequestCommand(body, file), ct);
        return FromResult(result, result.IsSuccess ? $"Đã gửi {result.Value.Id} — CS Việt An sẽ phản hồi sớm" : null);
    }

    /// <summary><c>{ text }</c> — khách trả lời yêu cầu đang "Chờ khách phản hồi" (chuyển lại "Đang xử lý", báo email cho CS).</summary>
    [HttpPost("{id}/reply")]
    [HasPermission(IdentityPermissions.SupportRequests)]
    [ProducesResponseType<ApiResponse<SupportRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Answer(string id, SupportAnswerRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new AnswerSupportRequestCommand(id, body.Text), ct), "Đã gửi phản hồi cho CS Việt An");
}

/// <summary>Trang quản trị trả lời và / hoặc đổi trạng thái yêu cầu.</summary>
internal sealed record SupportReplyRequest(string? Reply, string? Status);

/// <summary>Khách trả lời yêu cầu đang chờ.</summary>
internal sealed record SupportAnswerRequest(string? Text);
