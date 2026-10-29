using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>Đọc / khóa / xoá đơn chưa in trong dbo.MaVanDon và 2 bảng chi tiết.</summary>
internal static class DraftStore
{
    /// <summary>Đơn chưa in (bỏ bộ lọc ẩn nháp của LegacyOrder).</summary>
    public static IQueryable<LegacyOrder> Drafts(this ShipmentsDbContext db) =>
        db.LegacyOrders.IgnoreQueryFilters().Where(o => o.Status == LegacyOrder.DraftStatus);

    /// <summary>Kiện + dòng hàng của các đơn (theo MaVanDon.ID).</summary>
    public static async Task<(ILookup<int, LegacyPackageLine> Packages, ILookup<int, LegacyInvoiceLine> Items)> LinesAsync(
        ShipmentsDbContext db, IReadOnlyCollection<long> orderIds, CancellationToken ct)
    {
        var ids = orderIds.Where(id => id is > 0 and <= int.MaxValue).Select(id => (int)id).ToList();
        var packages = await db.LegacyPackageLines.AsNoTracking().Where(p => ids.Contains(p.OrderId)).OrderBy(p => p.Id).ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking()
            .Where(i => i.OrderId != null && ids.Contains(i.OrderId.Value)).OrderBy(i => i.Id).ToListAsync(ct);
        return (packages.ToLookup(p => p.OrderId), items.ToLookup(i => i.OrderId!.Value));
    }

    /// <summary>
    /// Khóa dòng MaVanDon chưa in tới hết transaction hiện tại (UPDLOCK: portal và hệ thống cũ cùng ghi dòng này phải chờ).
    /// false = đơn không còn ở trạng thái chưa in (vừa được in / xoá ở nơi khác).
    /// </summary>
    public static async Task<bool> LockAsync(ShipmentsDbContext db, long id, CancellationToken ct) =>
        await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM dbo.MaVanDon WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE ID = {id} AND Status = {LegacyOrder.DraftStatus}")
            .SingleAsync(ct) > 0;

    /// <summary>Xoá kiện / dòng hàng cũ của 1 đơn (sửa nháp ghi lại từ form, xoá nháp).</summary>
    public static async Task RemoveLinesAsync(ShipmentsDbContext db, long orderId, CancellationToken ct)
    {
        var id = checked((int)orderId);
        db.LegacyPackageLines.RemoveRange(await db.LegacyPackageLines.Where(p => p.OrderId == id).ToListAsync(ct));
        db.LegacyInvoiceLines.RemoveRange(await db.LegacyInvoiceLines.Where(i => i.OrderId == id).ToListAsync(ct));
    }
}
