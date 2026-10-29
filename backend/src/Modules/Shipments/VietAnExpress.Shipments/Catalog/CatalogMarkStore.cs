using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal static class CatalogMarkStore
{
    /// <summary>Đánh dấu 1 mặt hàng trong thư viện của khách (yêu thích / đã xoá).</summary>
    public static async Task<Result> MarkProductAsync(
        ShipmentsDbContext db, ICurrentUser user, DateTime now, string key, Action<CatalogMark, DateTime> apply, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return OrderErrors.CustomerRequired;
        if (!ProductLibrary.IsValidKey(key)) return CatalogMarkErrors.InvalidKey;
        await UpsertAsync(db, customerId, CatalogMark.Product, key, m => apply(m, now), now, ct);
        return Result.Success();
    }

    /// <summary>Lấy (hoặc tạo) đánh dấu của khách, áp thay đổi rồi lưu.</summary>
    public static async Task UpsertAsync(ShipmentsDbContext db, long customerId, string kind, string key, Action<CatalogMark> apply, DateTime now, CancellationToken ct)
    {
        var mark = await db.CatalogMarks.FirstOrDefaultAsync(m => m.CustomerId == customerId && m.Kind == kind && m.Key == key, ct);
        if (mark is null)
        {
            mark = CatalogMark.Create(customerId, kind, key, now);
            db.CatalogMarks.Add(mark);
        }
        apply(mark);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbConcurrency.IsUniqueViolation(ex))
        {
            // 2 thao tác cùng tạo đánh dấu này (2 tab / bấm 2 lần): dòng đã có → đọc lại và áp thay đổi lên dòng đó.
            db.Entry(mark).State = EntityState.Detached;
            var existing = await db.CatalogMarks.FirstAsync(m => m.CustomerId == customerId && m.Kind == kind && m.Key == key, ct);
            apply(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Đánh dấu của khách trên 1 mặt hàng (từ dbo.MatHangKhachHang).</summary>
internal sealed record ProductMark(bool IsFavorite, bool IsDeleted);
