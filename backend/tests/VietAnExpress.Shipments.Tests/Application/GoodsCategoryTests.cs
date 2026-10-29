using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class GoodsCategoryTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 10, 0, 0);

    [Theory]
    [InlineData("  Mỹ   phẩm  ", "Mỹ phẩm")]
    [InlineData("Đồ chơi", "Đồ chơi")]
    public void Ten_nhom_duoc_chuan_hoa(string input, string expected)
    {
        var result = GoodsCategoryRules.ValidateName(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null, "CATEGORY_NAME_REQUIRED")]
    [InlineData("   ", "CATEGORY_NAME_REQUIRED")]
    public void Ten_nhom_trong_bi_tu_choi(string? input, string code) =>
        Assert.Equal(code, GoodsCategoryRules.ValidateName(input).Error.Code);

    [Fact]
    public void Ten_nhom_qua_dai_bi_tu_choi() =>
        Assert.Equal("CATEGORY_NAME_TOO_LONG", GoodsCategoryRules.ValidateName(new string('A', GoodsCategory.NameMaxLength + 1)).Error.Code);

    [Fact]
    public void Nhom_cua_khach_dung_truoc_yeu_thich_len_dau_nhom_chung_giu_thu_tu()
    {
        var own = GoodsCategory.Create(200877, "Vải cuộn", isFavorite: false, Now);
        var favorite = GoodsCategory.Create(200877, "Áo dài", isFavorite: true, Now);

        var names = GoodsCategoryRules.Order([own, favorite]).Select(c => c.Name).ToList();

        Assert.Equal(["Áo dài", "Vải cuộn"], names);
        Assert.All(GoodsCategoryRules.Order([own]), c => Assert.True(c.IsOwn));
    }

    /// <summary>Nhóm chung Việt An (CustomerID null) như dòng do migration chèn.</summary>
    private static GoodsCategory Shared(long id, string name, int sortOrder)
    {
        var c = (GoodsCategory)Activator.CreateInstance(typeof(GoodsCategory), nonPublic: true)!;
        typeof(GoodsCategory).GetProperty(nameof(GoodsCategory.Id))!.SetValue(c, id);
        typeof(GoodsCategory).GetProperty(nameof(GoodsCategory.Name))!.SetValue(c, name);
        typeof(GoodsCategory).GetProperty(nameof(GoodsCategory.SortOrder))!.SetValue(c, sortOrder);
        return c;
    }

    [Fact]
    public void Nhom_chung_khach_danh_dau_yeu_thich_len_dau_cung_nhom_yeu_thich_cua_khach()
    {
        var own = GoodsCategory.Create(200877, "Vải cuộn", isFavorite: false, Now);
        var ownFavorite = GoodsCategory.Create(200877, "Áo dài", isFavorite: true, Now);
        var clothes = Shared(1, "Quần áo, giày dép", 1);
        var cosmetics = Shared(4, "Mỹ phẩm, hóa mỹ phẩm", 4);

        var list = GoodsCategoryRules.Order([clothes, own, cosmetics, ownFavorite], favoriteSharedIds: new HashSet<long> { 4 });

        Assert.Equal(["Áo dài", "Mỹ phẩm, hóa mỹ phẩm", "Vải cuộn", "Quần áo, giày dép"], list.Select(c => c.Name));
        Assert.Equal([true, true, false, false], list.Select(c => c.IsFavorite));
        Assert.False(list[1].IsOwn);
        Assert.False(GoodsCategoryRules.Order([cosmetics])[0].IsFavorite); // không có đánh dấu → không yêu thích
    }
}
