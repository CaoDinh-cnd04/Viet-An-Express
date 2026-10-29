using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Catalog;

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
