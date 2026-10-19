using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Application.Orders.Import;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Shipments.Api;

/// <summary>
/// Nhóm hàng hóa (dbo.NhomHangHoa) và khai invoice nhanh ở bước tạo đơn — dữ liệu lấy từ các dòng hàng khách đã khai (dbo.MaVanDon_ChiTietHang),
/// chỉ đơn của chính khách. Hợp đồng frontend: web/docs/API_CONTRACT.md §3.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}")]
[Tags("Tạo đơn")]
internal sealed class CatalogController : ApiControllerBase
{
    /// <summary>Nhóm hàng: nhóm của khách (yêu thích trước) rồi nhóm chung Việt An.</summary>
    [HttpGet("catalog/categories")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<GoodsCategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categories(CancellationToken ct) => OkData(await Sender.Send(new GetGoodsCategoriesQuery(), ct));

    /// <summary>Khách thêm nhóm hàng của mình.</summary>
    [HttpPost("catalog/categories")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<GoodsCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCategory(GoodsCategoryInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new SaveGoodsCategoryCommand(null, body), ct), "Đã thêm nhóm hàng");

    /// <summary>Sửa tên / đánh dấu yêu thích — chỉ nhóm khách tự tạo.</summary>
    [HttpPut("catalog/categories/{id:long}")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<GoodsCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCategory(long id, GoodsCategoryInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new SaveGoodsCategoryCommand(id, body), ct), "Đã cập nhật nhóm hàng");

    /// <summary>Xóa nhóm khách tự tạo (đơn đã khai nhóm này không bị ảnh hưởng).</summary>
    [HttpDelete("catalog/categories/{id:long}")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new DeleteGoodsCategoryCommand(id), ct), "Đã xóa nhóm hàng");

    /// <summary>Đánh dấu / bỏ yêu thích nhóm hàng — cả nhóm của khách lẫn nhóm chung Việt An (lưu riêng cho từng khách).</summary>
    [HttpPut("catalog/categories/{id:long}/favorite")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FavoriteCategory(long id, FavoriteInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new SetCategoryFavoriteCommand(id, body.IsFavorite), ct), body.IsFavorite ? "Đã thêm vào nhóm yêu thích" : "Đã bỏ yêu thích");

    /// <summary>
    /// Tra mã HS theo nước nhập khẩu (dbo.Catalog_HsCode): <c>country</c> US / GB / AU, <c>q</c> tên hàng hoặc mã số (≥ 2 ký tự),
    /// <c>mode</c> verified (mặc định, chỉ mã Việt An đã xác minh) hoặc catalog (toàn danh mục). Tối đa 50 dòng, không trả thuế suất.
    /// </summary>
    [HttpGet("catalog/hs-lookup")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<HsLookupItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> HsLookup([FromQuery] string? country, [FromQuery] string? q, [FromQuery] string? mode, CancellationToken ct) =>
        OkData(await Sender.Send(new HsLookupQuery(country, q, mode), ct));

    /// <summary>Thư viện mặt hàng: mỗi mặt hàng đã khai 1 dòng, đơn giá lần gần nhất; yêu thích lên đầu, mặt hàng đã xóa bị ẩn.</summary>
    [HttpGet("catalog/products")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SavedProductDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Products(CancellationToken ct) => OkData(await Sender.Send(new GetProductLibraryQuery(), ct));

    /// <summary>Đánh dấu / bỏ yêu thích mặt hàng (id = khóa mặt hàng trong thư viện).</summary>
    [HttpPut("catalog/products/{id}/favorite")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FavoriteProduct(string id, FavoriteInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new SetProductFavoriteCommand(id, body.IsFavorite), ct), body.IsFavorite ? "Đã thêm vào mặt hàng yêu thích" : "Đã bỏ yêu thích");

    /// <summary>Xóa mặt hàng khỏi thư viện (chỉ ẩn — đơn cũ giữ nguyên).</summary>
    [HttpDelete("catalog/products/{id}")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteProduct(string id, CancellationToken ct) =>
        FromResult(await Sender.Send(new DeleteProductCommand(id), ct), "Đã xóa mặt hàng khỏi thư viện");

    /// <summary>Invoice của các đơn gần đây (≤ 50) để chép lại.</summary>
    [HttpGet("invoices/recent")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecentInvoiceDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecentInvoices([FromQuery] int? limit, CancellationToken ct) =>
        OkData(await Sender.Send(new GetRecentInvoicesQuery(limit), ct));

    /// <summary>Tối đa 5 MB cho file mẫu invoice (49 dòng hàng).</summary>
    private const long InvoiceFileMaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Đọc file Excel mẫu invoice (Mau_Import_Invoice.xlsx, multipart <c>file</c>) → dòng hàng để điền vào bảng Invoice;
    /// không lưu gì. Dòng lỗi trả về trong <c>skipped</c> kèm số dòng Excel.
    /// </summary>
    [HttpPost("invoices/import")]
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
