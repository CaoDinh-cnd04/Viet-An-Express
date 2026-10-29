using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Import;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.AddressBook;

/// <summary>Sổ địa chỉ: người gửi / người nhận đã dùng ở các đơn trước của khách.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/orders")]
[Tags("Đơn hàng")]
internal sealed class AddressBookController : ApiControllerBase
{
    /// <summary>Người gửi đã dùng ở đơn trước, tên công ty / người liên hệ chứa <c>q</c> — gợi ý khi tài khoản con khai người gửi.</summary>
    [HttpGet("senders")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecentSenderDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Senders([FromQuery] string? q, CancellationToken ct) =>
        OkData(await Sender.Send(new GetRecentSendersQuery(q), ct));

    /// <summary>
    /// Người nhận đã gửi trước đây (mới nhất trước): tên công ty chứa <c>q</c> (tối đa 20) — hoặc không có <c>q</c> thì
    /// là sổ địa chỉ (100 người nhận gần nhất).
    /// </summary>
    [HttpGet("receivers")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecentReceiverDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Receivers([FromQuery] string? q, CancellationToken ct) =>
        OkData(await Sender.Send(new GetRecentReceiversQuery(q), ct));
}
