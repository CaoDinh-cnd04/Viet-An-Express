using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal sealed record GetRecentInvoicesQuery(int? Limit) : IRequest<IReadOnlyList<RecentInvoiceDto>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public int EffectiveLimit => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}

internal sealed class GetRecentInvoicesHandler(ShipmentsDbContext db, OrderAccess access)
    : IRequestHandler<GetRecentInvoicesQuery, IReadOnlyList<RecentInvoiceDto>>
{
    public async Task<IReadOnlyList<RecentInvoiceDto>> Handle(GetRecentInvoicesQuery q, CancellationToken ct)
    {
        var orders = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct));

        // Các đơn gần nhất có khai dòng hàng.
        var headers = await orders
            .Where(o => db.LegacyInvoiceLines.Any(l => l.OrderId != null && l.OrderId == o.Id))
            .OrderByDescending(o => o.Id)
            .Take(q.EffectiveLimit)
            .Select(LegacyOrderProjections.InvoiceHeader)
            .ToListAsync(ct);
        if (headers.Count == 0) return [];

        var byId = headers.ToDictionary(o => o.Id);
        var orderIds = headers.Select(o => (int)o.Id).ToList(); // ID ở bảng chi tiết là int.
        var lines = await db.LegacyInvoiceLines.AsNoTracking()
            .Where(l => l.OrderId != null && orderIds.Contains(l.OrderId.Value))
            .OrderByDescending(l => l.OrderId).ThenBy(l => l.Id)
            .ToListAsync(ct);

        return ProductLibrary.Recent(
            lines.Where(l => byId.ContainsKey(l.OrderId!.Value)).Select(l => new CustomerInvoiceLine(l, byId[l.OrderId!.Value])),
            q.EffectiveLimit);
    }
}
