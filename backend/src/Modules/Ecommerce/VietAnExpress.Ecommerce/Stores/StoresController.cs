using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.Ecommerce.Orders;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Ecommerce.Stores;

/// <summary>Shop khách kết nối trên kênh bán (dbo.KetNoiTMDT) — API_CONTRACT.md §5.</summary>
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

    /// <summary>Ngắt kết nối: xóa token, ẩn shop khỏi danh sách.</summary>
    [HttpDelete("stores/{id:long}")]
    [HasPermission(EcommercePermissions.Connect)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Disconnect(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new DisconnectStoreCommand(id), ct), "Đã ngắt kết nối cửa hàng");
}

internal sealed record StartConnectionRequest(string? Platform, string? ShopDomain, string? Region);
