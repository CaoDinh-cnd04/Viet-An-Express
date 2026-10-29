using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Import;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Printing;

/// <summary>In chứng từ (bill A4, invoice, CVCK, nhãn A6) và xuất bảng kê Excel của "Đơn hàng của tôi".</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/orders")]
[Tags("Đơn hàng")]
internal sealed class OrderPrintController : ApiControllerBase
{
    /// <summary>
    /// Trang in chứng từ (HTML, tự mở hộp thoại in): doc = bill-a4 | invoice | cvck | label-a6, hoặc nhiều loại
    /// ngăn bằng dấu phẩy (trang gộp, in lần lượt từng loại);
    /// bills = danh sách số vận đơn ngăn bằng dấu phẩy (tối đa 100).
    /// </summary>
    [HttpGet("print")]
    [HasPermission(ShipmentsPermissions.View)]
    [Produces("text/html")]
    public async Task<IActionResult> Print([FromQuery] string? bills, [FromQuery] string? doc, CancellationToken ct)
    {
        var list = (bills ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await Sender.Send(new PrintOrdersQuery(list, doc ?? ""), ct);
        return result.IsSuccess ? Content(result.Value, "text/html; charset=utf-8") : Problem(result.Error);
    }

    /// <summary>
    /// Xuất bảng kê gửi hàng (.xlsx) theo cùng bộ lọc với danh sách đơn; có page + pageSize thì chỉ xuất trang đó,
    /// không có thì xuất toàn bộ.
    /// </summary>
    [HttpGet("export")]
    [HasPermission(ShipmentsPermissions.View)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q, [FromQuery] string? searchField, [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal? weightFrom, [FromQuery] decimal? weightTo,
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? sortBy, [FromQuery] string? sortDir, CancellationToken ct)
    {
        var file = await Sender.Send(new ExportOrdersQuery(
            new GetOrdersQuery(q, searchField, status, type, fromDate, toDate, weightFrom, weightTo, page, pageSize, sortBy, sortDir)), ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }
}
