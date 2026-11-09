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

    [Theory]
    [InlineData("Xoài sấy Đà Nẵng", "XOAI SAY DA NANG")]
    [InlineData("Nguyễn Thị Uyên", "NGUYEN THI UYEN")]
    [InlineData("Chuyên tuyến|Japan", "CHUYEN TUYEN|JAPAN")]
    [InlineData("São Paulo", "SAO PAULO")]
    [InlineData("6010839/2", "6010839/2")]
    public void Fold_bo_dau_va_in_hoa(string input, string expected) => Assert.Equal(expected, OrderTextSearch.Fold(input));

    [Fact]
    public void Tu_khoa_chuan_bi_mau_LIKE_va_biet_co_chu_cai()
    {
        var t = OrderTextSearch.Term.Of("xoài 50%");
        Assert.Equal(("%xoài 50[%]%", "%XOAI 50[%]%", true), (t.Like, t.Upper, t.HasLetters));
        Assert.False(OrderTextSearch.Term.Of("6010839").HasLetters);
    }

    [Fact]
    public void Sap_xep_o_bo_nho_khop_thu_tu_SQL_null_dau_khoa_phu_ID()
    {
        OrderListRow R(long id, long? n = null, DateTime? d = null, string? t = null) => new(id, false, false, 1, 1, n, d, t);
        var rows = new[] { R(1, n: 5), R(2, n: null), R(3, n: 5), R(4, n: 2) };
        Assert.Equal([3L, 1, 4, 2], OrderListRows.Sort(rows, "bill", "desc").Select(r => r.Id));
        Assert.Equal([2L, 4, 1, 3], OrderListRows.Sort(rows, "bill", "asc").Select(r => r.Id));
        var names = new[] { R(1, t: "beta"), R(2, t: "Alpha "), R(3, t: null), R(4, t: "alpha") };
        Assert.Equal([3L, 2, 4, 1], OrderListRows.Sort(names, "cnee", "asc").Select(r => r.Id));
        Assert.Equal([4L, 3, 2, 1], OrderListRows.Sort(rows, null, "desc").Select(r => r.Id));
    }

    [Fact]
    public void Dan_nhieu_bill_tach_so_VA_ma_hang_va_ma_VA_Bill_khong_doc_ra_so()
    {
        var (numbers, codes, others) = OrderListFilter.ExactBillKeys(["6010839", "VAHCM6010532US", "6003585/2", "1234567890", "JD0146", "6010839"]);
        Assert.Equal([6010839L, 6010532, 6003585, 1234567890], numbers);
        Assert.Equal(["6010839", "VAHCM6010532US", "6003585", "1234567890", "JD0146"], codes);
        Assert.Equal(["JD0146"], others);
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
        Assert.Equal([("kh", "Saigonbay")], OrderListFilter.SearchTags("kh:Saigonbay", "all"));
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
