using System.Globalization;
using MediatR;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Orders;

internal sealed record GetOrderEventsQuery(string Bill) : IRequest<Result<IReadOnlyList<OrderEventDto>>>;

internal sealed class GetOrderEventsHandler(ShipmentsDbContext db, OrderAccess access, IHubEzTracking hubEz, IOrderTracking orderTracking)
    : IRequestHandler<GetOrderEventsQuery, Result<IReadOnlyList<OrderEventDto>>>
{
    public async Task<Result<IReadOnlyList<OrderEventDto>>> Handle(GetOrderEventsQuery q, CancellationToken ct)
    {
        var order = await OrderLookup.FindByBillAsync(db, access, q.Bill, ct, eventsOnly: true);
        if (order is null) return Result.Failure<IReadOnlyList<OrderEventDto>>(OrderErrors.NotFound(q.Bill));
        var events = LegacyOrderView.Events(order, hideSigner: false);
        // Hành trình hãng ở dbo.Order_Tracking (như trang tracking cũ) và HUB-EZ khi đơn đi HUB-EZ (HUB-EZ lỗi → giữ hành trình cũ).
        var number = order.OrderNumber?.ToString(CultureInfo.InvariantCulture);
        var legacy = number is null ? null : (await orderTracking.FindAsync([number], ct)).GetValueOrDefault(number);
        var hub = hubEz.Handles(order.ServiceName, order.BillConnect)
            ? (await hubEz.TrackAsync([order.BillConnect!], ct)).GetValueOrDefault(order.BillConnect!.Trim())
            : null;
        return Result.Success(HubEzEvents.Merge(events, LegacyTrackingMapper.Carrier(hub, legacy), !string.IsNullOrWhiteSpace(order.Pod)));
    }
}
