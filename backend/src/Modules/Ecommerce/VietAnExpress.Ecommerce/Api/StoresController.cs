using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Ecommerce.Application;
using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Ecommerce.Api;

/// <summary>Shop trên kênh bán và đơn E-commerce — API_CONTRACT.md §5, §5.1.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/ecom")]
[Tags("E-commerce")]
internal sealed class StoresController : ApiControllerBase
{
    /// <summary>Shop khách đã kết nối (không gồm shop đã ngắt).</summary>
    [HttpGet("stores")]
    [HasPermission(EcommercePermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<StoreConnectionDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => OkData(await Sender.Send(new GetStoreConnectionsQuery(), ct));

    /// <summary>Bắt đầu ủy quyền shop trên sàn — hiện chưa có kênh nào kết nối được (trả lỗi "chưa hỗ trợ").</summary>
    [HttpPost("stores/connect")]
    [HasPermission(EcommercePermissions.Connect)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Connect(StartConnectionRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new StartStoreConnectionCommand(body.Platform), ct), "Đã bắt đầu kết nối");

    /// <summary>Kéo ngay đơn mới từ sàn về dbo.DonTMDT — hiện chưa có kênh nào đồng bộ được.</summary>
    [HttpPost("stores/{id:long}/sync")]
    [HasPermission(EcommercePermissions.Orders)]
    [ProducesResponseType<SyncResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(long id, CancellationToken ct)
    {
        var result = await Sender.Send(new SyncStoreCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Đơn E-commerce của khách, lọc theo nguồn (src) và từ khóa (q) — mới nhất trước, tối đa 500.</summary>
    [HttpGet("orders")]
    [HasPermission(EcommercePermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<EcomOrderDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Orders([FromQuery] string? src, [FromQuery] string? q, [FromQuery] string? scope, CancellationToken ct) =>
        OkData(await Sender.Send(new GetEcomOrdersQuery(src, q, scope), ct));

    /// <summary>Khách nhập tay 1 đơn (1–5 sản phẩm) vào danh sách đơn E-commerce.</summary>
    [HttpPost("manual")]
    [HasPermission(EcommercePermissions.Orders)]
    [ProducesResponseType<ApiResponse<EcomOrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateManual(ManualOrderInput body, CancellationToken ct)
    {
        var result = await Sender.Send(new CreateManualOrderCommand(body), ct);
        return FromResult(result, result.IsSuccess ? $"Đã lưu đơn {result.Value.Ref}" : null);
    }

    /// <summary>Sửa đơn chưa có bill: người nhận, cân nặng, sản phẩm / mã HS, dịch vụ. Đồng bộ lại từ sàn không ghi đè dữ liệu đã sửa.</summary>
    [HttpPut("orders/{id:long}")]
    [HasPermission(EcommercePermissions.Orders)]
    [ProducesResponseType<ApiResponse<EcomOrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOrder(long id, EcomOrderEditInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new UpdateEcomOrderCommand(id, body), ct), "Đã lưu thay đổi");

    /// <summary>Xác nhận gửi → đơn chuyển sang "Đơn hàng của tôi" (đơn thiếu tên / địa chỉ / SĐT / sản phẩm bị bỏ qua).</summary>
    [HttpPost("orders/confirm")]
    [HasPermission(EcommercePermissions.Orders)]
    [ProducesResponseType<ConfirmOrdersResult>(StatusCodes.Status200OK)]
    public Task<IActionResult> Confirm(OrderIdsRequest body, CancellationToken ct) => SendConfirm(body, true, ct);

    /// <summary>Trả đơn đã xác nhận (chưa có bill) về tab Đơn hàng.</summary>
    [HttpPost("orders/unconfirm")]
    [HasPermission(EcommercePermissions.Orders)]
    [ProducesResponseType<ConfirmOrdersResult>(StatusCodes.Status200OK)]
    public Task<IActionResult> Unconfirm(OrderIdsRequest body, CancellationToken ct) => SendConfirm(body, false, ct);

    private async Task<IActionResult> SendConfirm(OrderIdsRequest body, bool confirm, CancellationToken ct)
    {
        var result = await Sender.Send(new ConfirmEcomOrdersCommand(body.Ids, confirm), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Xóa (ẩn) đơn chưa có bill — 1 hoặc nhiều đơn.</summary>
    [HttpPost("orders/delete")]
    [HasPermission(EcommercePermissions.Orders)]
    [ProducesResponseType<DeleteOrdersResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteOrders(OrderIdsRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new DeleteEcomOrdersCommand(body.Ids), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Ngắt kết nối: xóa token, ẩn shop khỏi danh sách.</summary>
    [HttpDelete("stores/{id:long}")]
    [HasPermission(EcommercePermissions.Connect)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Disconnect(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new DisconnectStoreCommand(id), ct), "Đã ngắt kết nối cửa hàng");
}

internal sealed record OrderIdsRequest(IReadOnlyList<string>? Ids);

internal sealed record StartConnectionRequest(string? Platform, string? ShopDomain, string? Region);
