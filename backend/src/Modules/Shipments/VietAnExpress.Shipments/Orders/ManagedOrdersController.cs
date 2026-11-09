using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Printing;

namespace VietAnExpress.Shipments.Orders;

/// <summary>
/// Nhân viên Việt An: đơn của mọi khách mình phụ trách (TCustomer.Staff_ID = USERS.ID; vai trò Administrator = mọi khách),
/// cùng tham số / dạng dữ liệu với "Đơn hàng của tôi" (/orders), mỗi dòng kèm <c>customer</c>. Chỉ xem, in, xuất —
/// tạo / copy đơn vẫn chọn khách rồi dùng trang của khách như cũ.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/vietan/orders")]
[Tags("Đơn hàng")]
internal sealed class ManagedOrdersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(ShipmentsPermissions.ManagedOrders)]
    [ProducesResponseType<OrderListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q, [FromQuery] string? searchField, [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal? weightFrom, [FromQuery] decimal? weightTo,
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? sortBy, [FromQuery] string? sortDir,
        CancellationToken ct) =>
        Ok(await Sender.Send(new GetOrdersQuery(q, searchField, status, type, fromDate, toDate, weightFrom, weightTo,
            page, pageSize, sortBy, sortDir, Managed: true), ct));

    [HttpGet("{bill}")]
    [HasPermission(ShipmentsPermissions.ManagedOrders)]
    [ProducesResponseType<ApiResponse<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string bill, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetOrderQuery(bill, Managed: true), ct));

    [HttpGet("{bill}/events")]
    [HasPermission(ShipmentsPermissions.ManagedOrders)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OrderEventDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Events(string bill, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetOrderEventsQuery(bill, Managed: true), ct));

    /// <summary>In chứng từ như /orders/print, trong phạm vi khách phụ trách.</summary>
    [HttpGet("print")]
    [HasPermission(ShipmentsPermissions.ManagedOrders)]
    [Produces("text/html")]
    public async Task<IActionResult> Print([FromQuery] string? bills, [FromQuery] string? doc, CancellationToken ct)
    {
        var list = (bills ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await Sender.Send(new PrintOrdersQuery(list, doc ?? "", Managed: true), ct);
        return result.IsSuccess ? Content(result.Value, "text/html; charset=utf-8") : Problem(result.Error);
    }

    /// <summary>Bảng kê như /orders/export, trong phạm vi khách phụ trách.</summary>
    [HttpGet("export")]
    [HasPermission(ShipmentsPermissions.ManagedOrders)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q, [FromQuery] string? searchField, [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal? weightFrom, [FromQuery] decimal? weightTo,
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? sortBy, [FromQuery] string? sortDir, CancellationToken ct)
    {
        var file = await Sender.Send(new ExportOrdersQuery(new GetOrdersQuery(
            q, searchField, status, type, fromDate, toDate, weightFrom, weightTo, page, pageSize, sortBy, sortDir, Managed: true)), ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }
}
