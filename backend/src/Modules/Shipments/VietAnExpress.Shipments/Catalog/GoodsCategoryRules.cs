using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal static class GoodsCategoryRules
{
    /// <summary>Kiểm tra tên; trả tên đã chuẩn hóa.</summary>
    public static Result<string> ValidateName(string? raw)
    {
        var name = GoodsCategory.NormalizeName(raw);
        if (name.Length == 0) return GoodsCategoryErrors.NameRequired;
        if (name.Length > GoodsCategory.NameMaxLength) return GoodsCategoryErrors.NameTooLong;
        return name;
    }

    /// <summary>
    /// Nhóm yêu thích (của khách hoặc nhóm chung khách đã đánh dấu) lên đầu; rồi nhóm của khách (theo tên),
    /// cuối cùng nhóm chung (theo thứ tự Việt An đặt).
    /// </summary>
    /// <param name="favoriteSharedIds">ID nhóm chung khách đánh dấu yêu thích (dbo.MatHangKhachHang).</param>
    public static IReadOnlyList<GoodsCategoryDto> Order(IEnumerable<GoodsCategory> categories, IReadOnlySet<long>? favoriteSharedIds = null) =>
        categories
            .Select(c => (Category: c, Favorite: IsFavorite(c, favoriteSharedIds)))
            .OrderByDescending(x => x.Favorite)
            .ThenBy(x => x.Category.CustomerId is null)
            .ThenBy(x => x.Category.CustomerId is null ? x.Category.SortOrder : 0)
            .ThenBy(x => x.Category.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => ToDto(x.Category, x.Favorite))
            .ToList();

    private static bool IsFavorite(GoodsCategory c, IReadOnlySet<long>? favoriteSharedIds) =>
        c.CustomerId is null ? favoriteSharedIds?.Contains(c.Id) == true : c.IsFavorite;

    public static GoodsCategoryDto ToDto(GoodsCategory c) => ToDto(c, c.IsFavorite);

    private static GoodsCategoryDto ToDto(GoodsCategory c, bool isFavorite) =>
        new(c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), c.Name, isFavorite, c.CustomerId is not null, []);
}
