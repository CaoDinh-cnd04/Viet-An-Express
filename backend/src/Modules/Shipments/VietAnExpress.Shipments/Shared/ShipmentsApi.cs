using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Shared;

internal sealed class ShipmentsApi(ShipmentsDbContext db) : IShipmentsApi
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
}
