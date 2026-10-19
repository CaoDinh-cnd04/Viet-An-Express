using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Domain;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

/// <summary>1 mặt hàng để khai invoice nhanh — khớp <c>SavedProduct</c> của frontend.</summary>
/// <param name="Id">Khóa mặt hàng (<see cref="ProductLibrary.KeyOf"/>) — dùng để đánh dấu yêu thích / xóa khỏi thư viện.</param>
internal sealed record SavedProductDto(string Id, string DescEn, string DescVi, string Manufacturer, string Origin, string Hs, string Unit, string Price, bool IsFavorite = false);

/// <summary>Đánh dấu của khách trên 1 mặt hàng (từ dbo.MatHangKhachHang).</summary>
internal sealed record ProductMark(bool IsFavorite, bool IsDeleted);

/// <summary>Body PUT /catalog/products/:id/favorite và /catalog/categories/:id/favorite.</summary>
internal sealed record FavoriteInput(bool IsFavorite);

internal static class CatalogMarkErrors
{
    public static readonly Error InvalidKey = Error.Validation("PRODUCT_KEY_INVALID", "Mã mặt hàng không hợp lệ");
}

/// <summary>Invoice của 1 đơn gần đây — khớp <c>RecentInvoice</c> của frontend.</summary>
internal sealed record RecentInvoiceDto(string Bill, string Cnee, string Date, string Currency, IReadOnlyList<SavedProductDto> Items);

/// <summary>1 dòng hàng kèm đơn chứa nó (đã lọc theo khách).</summary>
internal sealed record CustomerInvoiceLine(LegacyInvoiceLine Line, LegacyOrder Order);

/// <summary>Dựng thư viện mặt hàng / invoice cũ từ dbo.MaVanDon_ChiTietHang. Hàm thuần — có test.</summary>
internal static class ProductLibrary
{
    /// <summary>
    /// Mỗi mặt hàng (cùng tên EN, VN, HS, xuất xứ, đơn vị, nhà sản xuất — không phân biệt hoa thường) lấy 1 dòng,
    /// dùng đơn giá lần khai gần nhất; mặt hàng dùng gần đây đứng trước.
    /// </summary>
    /// <param name="lines">Dòng hàng, mới nhất trước.</param>
    /// <param name="marks">Đánh dấu của khách theo khóa mặt hàng: mặt hàng đã xóa bị ẩn, mặt hàng yêu thích lên đầu.</param>
    public static IReadOnlyList<SavedProductDto> Build(IEnumerable<LegacyInvoiceLine> lines, int max, IReadOnlyDictionary<string, ProductMark>? marks = null) =>
        lines
            .Where(l => !string.IsNullOrWhiteSpace(l.DescriptionEn) || !string.IsNullOrWhiteSpace(l.DescriptionVi))
            .Select(ToDto)
            .DistinctBy(p => p.Id)
            .Select(p => marks?.GetValueOrDefault(p.Id) is { } m ? (m.IsDeleted ? null : p with { IsFavorite = m.IsFavorite }) : p)
            .OfType<SavedProductDto>()
            .Take(max)
            .OrderByDescending(p => p.IsFavorite) // sắp xếp ổn định: trong mỗi nhóm giữ thứ tự dùng gần đây
            .ToList();

    /// <summary>
    /// Khóa mặt hàng: SHA-256 (hex thường, 64 ký tự) của tên EN, VN, HS, xuất xứ, đơn vị, nhà sản xuất —
    /// không phân biệt hoa thường, nên cùng mặt hàng luôn cho cùng khóa.
    /// </summary>
    public static string KeyOf(string descEn, string descVi, string hs, string origin, string unit, string manufacturer)
    {
        var text = string.Join('|', descEn, descVi, hs, origin, unit, manufacturer).ToUpperInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Khóa hợp lệ: đúng 64 ký tự hex thường.</summary>
    public static bool IsValidKey(string? key) => key is { Length: CatalogMark.KeyMaxLength } && key.All(char.IsAsciiHexDigitLower);

    /// <summary>Gom dòng hàng theo đơn; đơn mới nhất trước.</summary>
    /// <param name="lines">Dòng hàng, mới nhất trước.</param>
    public static IReadOnlyList<RecentInvoiceDto> Recent(IEnumerable<CustomerInvoiceLine> lines, int max) =>
        lines
            .GroupBy(x => x.Order.Id)
            .Take(max)
            .Select(g =>
            {
                var o = g.First().Order;
                var items = g.OrderBy(x => x.Line.Id).Select(x => ToDto(x.Line)).ToList();
                return new RecentInvoiceDto(
                    LegacyOrderView.BillOf(o),
                    o.ConsigneeName?.Trim() ?? "",
                    o.CreateDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                    Clean(g.First().Line.Currency) is { Length: > 0 } c ? c : Clean(o.Currency) is { Length: > 0 } oc ? oc : "USD",
                    items);
            })
            .ToList();

    public static SavedProductDto ToDto(LegacyInvoiceLine l)
    {
        var (en, vi, mfr, hs) = (Clean(l.DescriptionEn), Clean(l.DescriptionVi), Clean(l.Manufacturer), Clean(l.HsCode));
        var origin = Clean(l.Origin) is { Length: > 0 } o ? o : "VN";
        var unit = Clean(l.Unit) is { Length: > 0 } u ? u : "PCS";
        return new(
            KeyOf(en, vi, hs, origin, unit, mfr),
            en, vi, mfr, origin, hs, unit,
            l.UnitPrice is { } price ? price.ToString("0.##", CultureInfo.InvariantCulture) : "");
    }

    private static string Clean(string? value) => value?.Trim() ?? "";
}

// ---------- Thư viện mặt hàng (GET /catalog/products) ----------

internal sealed record GetProductLibraryQuery : IRequest<IReadOnlyList<SavedProductDto>>
{
    public const int MaxProducts = 300;

    /// <summary>Số dòng hàng gần nhất được xét — đủ cho khách gửi thường xuyên, không quét cả bảng.</summary>
    public const int ScanLines = 3000;
}

internal sealed class GetProductLibraryHandler(ShipmentsDbContext db, OrderAccess access, ICurrentUser user)
    : IRequestHandler<GetProductLibraryQuery, IReadOnlyList<SavedProductDto>>
{
    public async Task<IReadOnlyList<SavedProductDto>> Handle(GetProductLibraryQuery q, CancellationToken ct)
    {
        var orders = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct));
        var lines = await db.LegacyInvoiceLines.AsNoTracking()
            .Where(l => l.OrderId != null && orders.Any(o => o.Id == l.OrderId))
            .OrderByDescending(l => l.Id)
            .Take(GetProductLibraryQuery.ScanLines)
            .ToListAsync(ct);
        var marks = user.CustomerId is { } customerId
            ? await db.CatalogMarks.AsNoTracking()
                .Where(m => m.CustomerId == customerId && m.Kind == CatalogMark.Product)
                .ToDictionaryAsync(m => m.Key, m => new ProductMark(m.IsFavorite, m.IsDeleted), ct)
            : null;
        return ProductLibrary.Build(lines, GetProductLibraryQuery.MaxProducts, marks);
    }
}

// ---------- Yêu thích / xóa mặt hàng (PUT /catalog/products/:id/favorite, DELETE /catalog/products/:id) ----------

internal sealed record SetProductFavoriteCommand(string Key, bool IsFavorite) : IRequest<Result>;

/// <summary>Xóa khỏi thư viện = ẩn mặt hàng (dòng hàng trong đơn cũ giữ nguyên).</summary>
internal sealed record DeleteProductCommand(string Key) : IRequest<Result>;

internal sealed class ProductMarkHandlers(ShipmentsDbContext db, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<SetProductFavoriteCommand, Result>,
    IRequestHandler<DeleteProductCommand, Result>
{
    public Task<Result> Handle(SetProductFavoriteCommand cmd, CancellationToken ct) =>
        Mark(cmd.Key, (m, now) => m.SetFavorite(cmd.IsFavorite, now), ct);

    public Task<Result> Handle(DeleteProductCommand cmd, CancellationToken ct) =>
        Mark(cmd.Key, (m, now) => m.MarkDeleted(now), ct);

    private async Task<Result> Mark(string key, Action<CatalogMark, DateTime> apply, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return OrderErrors.CustomerRequired;
        if (!ProductLibrary.IsValidKey(key)) return CatalogMarkErrors.InvalidKey;
        var now = VietnamTime.Now(clock);
        await CatalogMarkStore.UpsertAsync(db, customerId, CatalogMark.Product, key, m => apply(m, now), now, ct);
        return Result.Success();
    }
}

internal static class CatalogMarkStore
{
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

// ---------- Invoice cũ (GET /invoices/recent) ----------

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
