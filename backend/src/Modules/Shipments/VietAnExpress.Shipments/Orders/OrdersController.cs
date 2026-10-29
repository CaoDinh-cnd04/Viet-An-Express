using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Import;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Printing;

namespace VietAnExpress.Shipments.Orders;

/// <summary>
/// "Đơn hàng của tôi" — đọc bảng vận đơn hệ thống cũ dbo.MaVanDon (danh sách, chi tiết, hành trình, copy đơn).
/// Dạng dữ liệu theo hợp đồng frontend: web/docs/API_CONTRACT.md §1.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/orders")]
[Tags("Đơn hàng")]
internal sealed class OrdersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<OrderListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q, [FromQuery] string? searchField, [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal? weightFrom, [FromQuery] decimal? weightTo,
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? sortBy, [FromQuery] string? sortDir,
        CancellationToken ct) =>
        Ok(await Sender.Send(new GetOrdersQuery(q, searchField, status, type, fromDate, toDate, weightFrom, weightTo,
            page, pageSize, sortBy, sortDir), ct));

    /// <summary>Chi tiết 1 đơn theo số VA hoặc mã hãng.</summary>
    [HttpGet("{bill}")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string bill, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetOrderQuery(bill), ct));

    [HttpGet("{bill}/events")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OrderEventDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Events(string bill, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetOrderEventsQuery(bill), ct));

    /// <summary>Toàn bộ form để copy đơn; chỉ đọc, giữ phạm vi khách/tài khoản con của đơn gốc.</summary>
    [HttpGet("{bill}/copy")]
    [HasPermission(ShipmentsPermissions.View)]
    [HasPermission(ShipmentsPermissions.Create)]
    public async Task<IActionResult> Copy(string bill, CancellationToken ct) => FromResult(await Sender.Send(new CopyOrderQuery(bill), ct));
}
