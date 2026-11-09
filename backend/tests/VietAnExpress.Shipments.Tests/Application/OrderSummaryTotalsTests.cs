using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class OrderSummaryTotalsTests
{
    // 4 nhóm (chưa đi?, chưa phát?): chưa đi 3 đơn, đã đi 16, chưa phát 12, đã phát 7 — các tab trùng nhau.
    private static readonly OrderSummaryQuery.Group[] Groups =
    [
        new(Waiting: true, NotDelivered: true, Count: 3, Pieces: 4, Weight: 2.5m),
        new(Waiting: false, NotDelivered: true, Count: 9, Pieces: 13, Weight: 18m),
        new(Waiting: false, NotDelivered: false, Count: 7, Pieces: 13, Weight: 20m)
    ];

    [Fact]
    public void Dem_tung_tab_tren_tap_chua_loc_tab()
    {
        var t = OrderSummaryQuery.Summarize(Groups, ["ok"]);
        Assert.Equal(new Dictionary<string, int> { ["all"] = 19, ["wait"] = 3, ["fly"] = 16, ["nd"] = 12, ["ok"] = 7 }, t.Counts);
    }

    [Fact]
    public void Khong_loc_hoac_chon_du_4_tab_thi_lay_tong_chung()
    {
        Assert.Equal((19, 30, 40.5m), Tot(OrderSummaryQuery.Summarize(Groups, [])));
        Assert.Equal((19, 30, 40.5m), Tot(OrderSummaryQuery.Summarize(Groups, ["wait", "fly", "nd", "ok"])));
    }

    [Fact]
    public void Loc_1_tab_thi_lay_tong_cua_tab_do()
    {
        Assert.Equal((3, 4, 2.5m), Tot(OrderSummaryQuery.Summarize(Groups, ["wait"])));
        Assert.Equal((7, 13, 20m), Tot(OrderSummaryQuery.Summarize(Groups, ["ok"])));
    }

    [Fact]
    public void Loc_2_tab_khac_nhom_thi_lay_phan_giao_khong_cong_trung()
    {
        Assert.Equal((9, 13, 18m), Tot(OrderSummaryQuery.Summarize(Groups, ["fly", "nd"]))); // đã đi VÀ chưa phát
        Assert.Equal((16, 26, 38m), Tot(OrderSummaryQuery.Summarize(Groups, ["fly"])));
    }

    [Theory]
    [InlineData("wait")]
    [InlineData("fly")]
    [InlineData("nd")]
    [InlineData("ok")]
    [InlineData("wait,ok")]
    [InlineData("fly,nd")]
    [InlineData("wait,fly,nd")]
    public void Co_trang_thai_tinh_o_bo_nho_khop_dieu_kien_SQL(string status)
    {
        var statuses = OrderListFilter.Statuses(status);
        var today = new DateTime(2026, 11, 6);
        foreach (var sent in new DateTime?[] { null, today })
        foreach (var pod in new[] { null, "", "   ", "05/11/2026 10:30, DELIVERED" })
        {
            var o = new LegacyOrder { SentDate = sent, Pod = pod };
            var sql = OrderListFilter.ApplyStatus(new[] { o }.AsQueryable(), status, today).Any();
            // SQL Server coi "   " = "" (bỏ khoảng trắng cuối) — cờ chưa phát của truy vấn gom nhóm cũng vậy.
            var notDelivered = pod is null || pod.TrimEnd(' ').Length == 0;
            var memory = OrderListFilter.MatchesStatus(statuses, sent is null, notDelivered);
            if (pod != "   ") Assert.Equal(sql, memory);
        }
    }

    private static (int, int, decimal) Tot(OrderSummaryQuery.Totals t) => (t.Total, t.Pieces, t.Weight);
}
