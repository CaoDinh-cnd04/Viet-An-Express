using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Import;

/// <summary>Đọc file Excel mẫu invoice để điền nhanh bảng Invoice ở bước tạo đơn (không lưu gì).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/invoices")]
[Tags("Tạo đơn")]
internal sealed class InvoiceImportController : ApiControllerBase
{
    /// <summary>Tối đa 5 MB cho file mẫu invoice (49 dòng hàng).</summary>
    private const long InvoiceFileMaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Đọc file Excel mẫu invoice (Mau_Import_Invoice.xlsx, multipart <c>file</c>) → dòng hàng để điền vào bảng Invoice;
    /// không lưu gì. Dòng lỗi trả về trong <c>skipped</c> kèm số dòng Excel.
    /// </summary>
    [HttpPost("import")]
    [HasPermission(ShipmentsPermissions.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(InvoiceFileMaxBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<InvoiceImportResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportInvoice(IFormFile? file, CancellationToken ct)
    {
        if (file is not { Length: > 0 }) return Problem(Error.Validation("INVOICE_FILE_EMPTY", "Chọn file Excel invoice theo mẫu"));
        if (file.Length > InvoiceFileMaxBytes) return Problem(Error.Validation("INVOICE_FILE_TOO_LARGE", "File tối đa 5 MB"));
        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        SheetData sheet;
        try { sheet = ExcelSheetReader.Read(stream); }
        catch (InvalidDataException e) { return Problem(Error.Validation("INVOICE_FILE_INVALID", e.Message)); }
        if (!InvoiceExcelParser.HasRequiredColumns(sheet.Headers))
            return Problem(Error.Validation("INVOICE_FILE_TEMPLATE", "File không đúng mẫu — cần các cột Product_en, Quantyti, Unit_Price (tải file mẫu để xem)"));
        var result = InvoiceExcelParser.Parse(sheet);
        return OkData(result, $"Đọc được {result.Items.Count} mặt hàng");
    }
}
