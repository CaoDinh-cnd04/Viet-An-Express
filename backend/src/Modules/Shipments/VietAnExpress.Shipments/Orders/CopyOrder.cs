using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Orders;

internal sealed record CopyOrderQuery(string Bill) : IRequest<Result<JsonElement>>;

/// <summary>Return form data only; the new order never inherits the source bill, tracking or delivery state.</summary>
internal sealed class CopyOrderHandler(ShipmentsDbContext db, OrderAccess access) : IRequestHandler<CopyOrderQuery, Result<JsonElement>>
{
    public async Task<Result<JsonElement>> Handle(CopyOrderQuery q, CancellationToken ct)
    {
        var code = q.Bill.Trim();
        var number = VaBillCode.Number(code);
        var scope = await access.ScopeAsync(ct);
        var orders = access.Apply(db.LegacyOrders.AsNoTracking(), scope);
        orders = number is { } parsed ? orders.Where(o => o.OrderNumber == parsed || o.BillConnect == code) : orders.Where(o => o.BillConnect == code);
        var order = await orders.OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        if (order is null) return OrderErrors.NotFound(q.Bill);

        var id = order.Id is > 0 and <= int.MaxValue ? (int)order.Id : 0;
        var packages = await db.LegacyPackageLines.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).ToListAsync(ct);
        return JsonSerializer.SerializeToElement(OrderCopy.FromLegacy(order, packages, items), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
