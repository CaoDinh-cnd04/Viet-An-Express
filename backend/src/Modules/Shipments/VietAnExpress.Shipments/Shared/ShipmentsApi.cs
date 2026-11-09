using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Shared;

internal sealed class ShipmentsApi(ShipmentsDbContext db, TimeProvider clock) : IShipmentsApi
{
    public async Task<BillSummary?> FindBillAsync(long customerId, string bill, CancellationToken cancellationToken = default)
    {
        var code = bill.Trim().ToUpperInvariant();
        if (code.Length == 0) return null;
        long? number = long.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        var order = await db.LegacyOrders.AsNoTracking()
            .Where(o => o.CustomerId == customerId
                && ((o.VaBill != null && o.VaBill.ToUpper() == code)
                    || (number != null && o.OrderNumber == number)
                    || (o.BillConnect != null && o.BillConnect.ToUpper() == code)))
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return order is null ? null : new BillSummary(LegacyOrderView.BillOf(order), order.ConsigneeName?.Trim(), order.ConsigneeCountry?.Trim());
    }

    public async Task<IReadOnlyList<CustomerOrderActivity>> GetCustomerActivityAsync(IReadOnlyCollection<long>? customerIds, int take,
        CancellationToken cancellationToken = default)
    {
        if (customerIds is { Count: 0 } || take <= 0) return [];
        var since = VietnamTime.ToVietnam(clock.GetUtcNow()).Date.AddDays(-CustomerOrderActivity.WindowDays);
        var result = new List<CustomerOrderActivity>();
        // Cả đơn nháp (Status 0): khách mới thường mới chỉ có nháp. Chia lô vì SQL Server giới hạn ~2100 tham số.
        var chunks = customerIds is null ? new long[]?[] { null } : customerIds.Distinct().Chunk(1000).ToArray<long[]?>();
        foreach (var chunk in chunks)
        {
            var orders = db.LegacyOrders.IgnoreQueryFilters().AsNoTracking()
                .Where(o => o.CustomerId != null && o.CustomerId > 0 && o.CreateDate >= since);
            if (chunk is not null) orders = orders.Where(o => chunk.Contains(o.CustomerId!.Value));
            var rows = await orders
                .GroupBy(o => o.CustomerId!.Value)
                .Select(g => new { g.Key, Last = g.Max(o => o.CreateDate), LastId = g.Max(o => o.Id), Count = g.Count() })
                .OrderByDescending(x => x.LastId)
                .Take(take)
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(x => new CustomerOrderActivity(x.Key, x.Last, x.LastId, x.Count)));
        }
        return result.OrderByDescending(x => x.LastOrderId).Take(take).ToList();
    }
}
