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

internal sealed class GetOrderEventsHandler(ShipmentsDbContext db, OrderAccess access, IHubEzTracking hubEz)
    : IRequestHandler<GetOrderEventsQuery, Result<IReadOnlyList<OrderEventDto>>>
{
    public async Task<Result<IReadOnlyList<OrderEventDto>>> Handle(GetOrderEventsQuery q, CancellationToken ct)
    {
        var order = await OrderLookup.FindByBillAsync(db, access, q.Bill, ct, eventsOnly: true);
        if (order is null) return Result.Failure<IReadOnlyList<OrderEventDto>>(OrderErrors.NotFound(q.Bill));
        var events = LegacyOrderView.Events(order, hideSigner: false);
        if (!hubEz.Handles(order.ServiceName, order.BillConnect)) return Result.Success(events);
        // Đơn đi HUB-EZ: thêm hành trình của HUB-EZ theo mã hãng (HUB-EZ lỗi → giữ hành trình cũ).
        var tracks = await hubEz.TrackAsync([order.BillConnect!], ct);
        return Result.Success(HubEzEvents.Merge(events, tracks.GetValueOrDefault(order.BillConnect!.Trim()), !string.IsNullOrWhiteSpace(order.Pod)));
    }
}
