using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>
/// Tìm vận đơn (chỉ đọc) trong dbo.MaVanDon cho tra cứu công khai.
/// Khớp theo số VA (OrderNumber), mã hãng (Bill_Connect), AWB hoặc mã bill của khách.
/// </summary>
internal interface ILegacyBillReader
{
    /// <param name="customerId">Có giá trị = chỉ tìm trong đơn của khách này (trang MyTracking của khách).</param>
    Task<IReadOnlyList<LegacyOrder>> FindAsync(IReadOnlyList<string> bills, CancellationToken cancellationToken, long? customerId = null);
}

internal sealed class LegacyBillReader(ShipmentsDbContext db, ILogger<LegacyBillReader> logger) : ILegacyBillReader
{
    public async Task<IReadOnlyList<LegacyOrder>> FindAsync(IReadOnlyList<string> bills, CancellationToken cancellationToken, long? customerId = null)
    {
        if (bills.Count == 0) return [];

        var numbers = bills
            .Select(b => VaBillCode.Number(b))
            .OfType<long>()
            .ToList();
        try
        {
            var orders = db.LegacyOrders.AsNoTracking();
            if (customerId is { } owner) orders = orders.Where(o => o.CustomerId == owner);
            return await orders
                .Where(o => (o.OrderNumber != null && numbers.Contains(o.OrderNumber.Value))
                    || (o.BillConnect != null && bills.Contains(o.BillConnect))
                    || (o.Awb != null && bills.Contains(o.Awb))
                    || (o.CustomerBill != null && bills.Contains(o.CustomerBill)))
                .Select(LegacyOrderProjections.Tracking)
                .ToListAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // 208 = không có bảng (database mới / môi trường test) → coi như không có vận đơn cũ.
            logger.LogWarning("Không có bảng dbo.MaVanDon — bỏ qua tra cứu vận đơn cũ");
            return [];
        }
    }
}
