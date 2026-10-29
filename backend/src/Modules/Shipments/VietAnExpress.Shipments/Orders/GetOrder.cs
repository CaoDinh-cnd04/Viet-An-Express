using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Orders;

internal sealed record GetOrderQuery(string Bill) : IRequest<Result<OrderDto>>;

internal sealed class GetOrderHandler(ShipmentsDbContext db, OrderAccess access, TimeProvider clock)
    : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery q, CancellationToken ct)
    {
        var order = await OrderLookup.FindByBillAsync(db, access, q.Bill, ct);
        if (order is null) return OrderErrors.NotFound(q.Bill);

        // Kiện + dòng hàng invoice nằm ở 2 bảng chi tiết, nối theo MaVanDon.ID (cột int).
        var id = order.Id is > 0 and <= int.MaxValue ? (int)order.Id : 0;
        var packages = await db.LegacyPackageLines.AsNoTracking().Where(p => p.OrderId == id).OrderBy(p => p.Id).ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking().Where(i => i.OrderId == id).OrderBy(i => i.Id).ToListAsync(ct);
        return LegacyOrderView.ToDetailDto(order, VietnamTime.ToVietnam(clock.GetUtcNow()).Date, packages, items);
    }
}
