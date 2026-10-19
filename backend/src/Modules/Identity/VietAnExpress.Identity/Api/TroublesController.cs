using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Domain;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>
/// Hỗ trợ &amp; sự cố của khách (dbo.TContact): gửi sự cố vận đơn / yêu cầu hỗ trợ / góp ý, xem danh sách và trạng thái.
/// CS xử lý ở phần mềm cũ / trang /admin; đổi trạng thái thì khách nhận thông báo.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/troubles")]
[Tags("Hỗ trợ & sự cố")]
internal sealed class TroublesController : ApiControllerBase
{
    private const long MaxRequestBytes = Trouble.FileMaxBytes + 1024 * 1024;

    [HttpGet]
    [HasPermission(IdentityPermissions.Troubles)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TroubleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => FromResult(await Sender.Send(new GetTroublesQuery(), ct));

    /// <summary>
    /// multipart/form-data: <c>kind</c> (su_co / ho_tro / gop_y), <c>bill</c>, <c>cnee</c>, <c>ct</c>, <c>type</c>, <c>subject</c>, <c>lv</c>,
    /// <c>desc</c>, <c>req</c>, <c>contact</c>, <c>file?</c> (≤ 10 MB: ảnh, PDF, Excel, CSV, Word — chỉ gửi kèm email, không lưu database).
    /// </summary>
    [HttpPost]
    [HasPermission(IdentityPermissions.Troubles)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<ApiResponse<TroubleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromForm] NewTroubleRequest body, CancellationToken ct)
    {
        TroubleFile? file = null;
        if (Request.Form.Files.GetFile("file") is { } upload)
        {
            if (upload.Length > Trouble.FileMaxBytes) return Problem(TroubleErrors.FileTooLarge);
            await using var stream = upload.OpenReadStream();
            using var buffer = new MemoryStream((int)upload.Length);
            await stream.CopyToAsync(buffer, ct);
            file = new TroubleFile(upload.FileName, string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType, buffer.ToArray());
        }
        var result = await Sender.Send(new CreateTroubleCommand(body, file), ct);
        return FromResult(result, result.IsSuccess ? $"Đã gửi {result.Value.Id} — CS Việt An sẽ phản hồi sớm" : null);
    }
}

internal sealed record TroubleReplyRequest(string? Reply, string? Status);
