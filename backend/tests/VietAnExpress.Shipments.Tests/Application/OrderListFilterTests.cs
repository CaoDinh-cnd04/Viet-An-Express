using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Lọc nhiều giá trị ở "Đơn hàng của tôi": dán nhiều mã / tên, chọn nhiều trạng thái.</summary>
public class OrderListFilterTests
{
    private static readonly DateTime Today = new(2026, 9, 30);

    [Fact]
    public void SearchTerms_tach_theo_dong_bo_trong_bo_trung()
    {
        Assert.Equal(["6010839", "6010532", "LINEX CO., LTD"], OrderListFilter.SearchTerms("6010839\r\n 6010532 \n\n6010839\nLINEX CO., LTD"));
        Assert.Empty(OrderListFilter.SearchTerms("  \n "));
        Assert.Empty(OrderListFilter.SearchTerms(null));
    }

    [Fact]
    public void SearchTerms_gioi_han_so_gia_tri()
    {
        var many = string.Join('\n', Enumerable.Range(0, OrderListFilter.MaxTerms + 10).Select(i => $"B{i}"));
        Assert.Equal(OrderListFilter.MaxTerms, OrderListFilter.SearchTerms(many).Count);
    }

    [Fact]
    public void SearchTags_doc_tien_to_truong_cua_tung_the()
    {
        var tags = OrderListFilter.SearchTags("cnee:Ms Uyen\nct: Singapore\n6010839\nfoo:bar", "bill");
        Assert.Equal(
            [("cnee", "Ms Uyen"), ("ct", "Singapore"), ("bill", "6010839"), ("bill", "foo:bar")],
            tags);
        Assert.Equal([("all", "x")], OrderListFilter.SearchTags("x", "khong-hop-le"));
    }

    [Theory]
    [InlineData("all", new string[0])]
    [InlineData("wait,fly", new[] { "wait", "fly" })]
    [InlineData(" ok , abc,ok", new[] { "ok" })]
    public void Statuses_doc_danh_sach(string input, string[] expected) =>
        Assert.Equal(expected, OrderListFilter.Statuses(input));

    [Fact]
    public void ApplyStatus_trong_nhom_khop_bat_ky_giua_2_nhom_phai_khop_ca_hai()
    {
        var orders = new[]
        {
            new LegacyOrder { Id = 1 },                                                                    // chưa đi, chưa phát
            new LegacyOrder { Id = 2, SentDate = Today.AddDays(-1), PodEstimate = Today.AddDays(2) },      // đã đi, chưa phát
            new LegacyOrder { Id = 3, SentDate = Today.AddDays(-5), Pod = "30/09/2026 10:00 delivered" }   // đã đi, đã phát
        }.AsQueryable();
        long[] Ids(string status) => OrderListFilter.ApplyStatus(orders, status, Today).Select(o => o.Id).ToArray();

        Assert.Equal([2], Ids("fly,nd"));          // đang trên đường: đã đi VÀ chưa phát
        Assert.Equal([1, 2], Ids("wait,fly,nd"));  // đủ 2 ô hành trình = không lọc hành trình, chỉ còn chưa phát
        Assert.Equal([2, 3], Ids("fly"));
        Assert.Equal([1, 2], Ids("nd"));
        Assert.Empty(Ids("wait,ok"));
        Assert.Equal(3, OrderListFilter.ApplyStatus(orders, "all", Today).Count());
    }
}
