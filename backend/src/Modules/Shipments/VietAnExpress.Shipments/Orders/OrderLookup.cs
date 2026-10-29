using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Orders;

/// <summary>Tìm 1 vận đơn của khách (chi tiết đơn, hành trình).</summary>
internal static class OrderLookup
{
    /// <summary>Tìm theo số VA hoặc mã hãng, trong phạm vi khách của người dùng.</summary>
    public static async Task<LegacyOrder?> FindByBillAsync(ShipmentsDbContext db, OrderAccess access, string bill, CancellationToken ct, bool eventsOnly = false)
    {
        var code = bill.Trim();
        var number = VaBillCode.Number(code);
        var query = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct));
        // Mã hãng không phải số không được khớp nhầm tất cả dòng OrderNumber NULL.
        query = number is { } parsed
            ? query.Where(o => o.OrderNumber == parsed || o.BillConnect == code)
            : query.Where(o => o.BillConnect == code);
        query = query.OrderByDescending(o => o.Id);
        if (eventsOnly) query = query.Select(LegacyOrderProjections.Events);
        return await query.FirstOrDefaultAsync(ct);
    }
}
