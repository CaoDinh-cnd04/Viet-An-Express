using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Application.Orders.Documents;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

/// <summary>Ghi / đọc chi tiết kiện (dbo.MaVanDon_PCS_DIM) và dòng hàng invoice (dbo.MaVanDon_ChiTietHang) theo MaVanDon.ID.</summary>
internal static class LegacyOrderLinesWriter
{
    /// <summary>
    /// Gọi sau khi đã SaveChanges các đơn (có MaVanDon.ID), trong cùng transaction với việc ghi đơn.
    /// </summary>
    public static async Task AddAsync(ShipmentsDbContext db, IEnumerable<(LegacyOrder Order, OrderPayload Payload)> saved, CancellationToken ct)
    {
        foreach (var (order, payload) in saved)
        {
            var orderId = checked((int)order.Id); // cột MaVanDonID kiểu int
            db.LegacyPackageLines.AddRange(LegacyOrderFactory.PackageLines(payload, orderId));
            db.LegacyInvoiceLines.AddRange(LegacyOrderFactory.InvoiceLines(payload, orderId));
        }
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Chi tiết kiện + dòng hàng đọc từ 2 bảng chi tiết, theo MaVanDon.ID — dùng cho in bill / invoice.</summary>
internal static class LegacyOrderLinesReader
{
    public static async Task<Dictionary<long, (IReadOnlyList<PrintItem> Items, IReadOnlyList<PrintPackage> Packages)>> LoadAsync(
        ShipmentsDbContext db, IReadOnlyCollection<LegacyOrder> orders, CancellationToken ct)
    {
        var ids = orders.Where(o => o.Id is > 0 and <= int.MaxValue).Select(o => (int)o.Id).Distinct().ToList();
        if (ids.Count == 0) return [];

        var packages = await db.LegacyPackageLines.AsNoTracking()
            .Where(p => ids.Contains(p.OrderId))
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking()
            .Where(i => i.OrderId != null && ids.Contains(i.OrderId.Value))
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        var result = new Dictionary<long, (IReadOnlyList<PrintItem>, IReadOnlyList<PrintPackage>)>();
        foreach (var id in ids)
        {
            var p = packages.Where(x => x.OrderId == id).Select(ToPrint).ToList();
            var i = items.Where(x => x.OrderId == id).Select(ToPrint).ToList();
            if (p.Count > 0 || i.Count > 0) result[id] = (i, p);
        }
        return result;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Cột TrongLuong là tổng cân của dòng → cân 1 kiện = TrongLuong / SoLuong.</summary>
    public static PrintPackage ToPrint(LegacyPackageLine p) =>
        new(p.Quantity, p.LengthCm, p.WidthCm, p.HeightCm,
            p.Quantity > 0 ? decimal.Round(p.WeightKg / p.Quantity, 3, MidpointRounding.AwayFromZero) : p.WeightKg, p.VolumetricKg);

    public static PrintItem ToPrint(LegacyInvoiceLine i)
    {
        var en = i.DescriptionEn?.Trim() ?? "";
        var vi = i.DescriptionVi?.Trim() ?? "";
        return new PrintItem(en.Length == 0 ? vi : en, i.Quantity ?? 0, string.IsNullOrWhiteSpace(i.Unit) ? "PCS" : i.Unit.Trim(), i.UnitPrice ?? 0,
            Blank(i.HsCode), Blank(i.Origin), en.Length == 0 ? null : Blank(vi), Blank(i.Manufacturer), i.Package);
    }
}
