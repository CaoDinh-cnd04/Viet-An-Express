using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Import;

/// <summary>Tạo đơn từ file Excel mẫu Mau_Excel_Tao_Don.xlsx (kiểm tra trước, rồi tạo các dòng hợp lệ).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/orders")]
[Tags("Đơn hàng")]
internal sealed class OrderImportController : ApiControllerBase
{
    /// <summary>
    /// Kiểm tra file Excel tạo đơn (mẫu Mau_Excel_Tao_Don.xlsx) — trả từng dòng kèm lỗi / cảnh báo, chưa tạo đơn.
    /// Form: file (.xlsx), service, hub (tuỳ chọn — đã chọn service thì bắt buộc hub).
    /// </summary>
    [HttpPost("import/preview")]
    [HasPermission(ShipmentsPermissions.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportOrdersCommand.MaxBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<ImportResultDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ImportPreview([FromForm] ImportForm form, CancellationToken ct) => Import(form, commit: false, ct);

    /// <summary>Tạo đơn cho các dòng hợp lệ của file (≤ 100 dòng) — ghi dbo.MaVanDon, cấp số vận đơn ngay; dòng lỗi bị bỏ qua.</summary>
    [HttpPost("import")]
    [HasPermission(ShipmentsPermissions.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportOrdersCommand.MaxBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<ImportResultDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ImportCommit([FromForm] ImportForm form, CancellationToken ct) => Import(form, commit: true, ct);

    private async Task<IActionResult> Import(ImportForm form, bool commit, CancellationToken ct)
    {
        if (form.File is not { Length: > 0 } file) return Problem(ImportErrors.Empty);
        await using var stream = file.OpenReadStream();
        var result = await Sender.Send(
            new ImportOrdersCommand(stream, file.FileName, file.Length, form.Service, form.Hub, form.Branch, commit), ct);
        var message = !result.IsSuccess ? null
            : commit ? $"Đã tạo {result.Value.Created} đơn"
            : $"{result.Value.Valid}/{result.Value.Total} dòng hợp lệ";
        return FromResult(result, message);
    }
}

/// <summary>Form tải file tạo đơn từ Excel.</summary>
internal sealed class ImportForm
{
    public IFormFile? File { get; set; }
    public string? Service { get; set; }
    public string? Hub { get; set; }
    public string? Branch { get; set; }
}
