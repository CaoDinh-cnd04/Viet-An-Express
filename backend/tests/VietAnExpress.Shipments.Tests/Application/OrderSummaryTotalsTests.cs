using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class OrderSummaryTotalsTests
{
    // 19 đơn: chưa đi 3 đơn (4 kiện, 2,5 kg), đã đi 16, chưa phát 12, đã phát 7 (các nhóm trùng nhau).
    private static readonly OrderSummaryQuery.Totals Totals = new(19, 3, 16, 12, 7, 30, 40.5m,
        4, 2.5m, 26, 38m, 17, 20.5m, 13, 20m);

    [Fact]
    public void Khong_loc_hoac_chon_du_4_nhom_thi_lay_tong_chung()
    {
        Assert.Equal((30, 40.5m), Totals.For([]));
        Assert.Equal((30, 40.5m), Totals.For(["wait", "fly", "nd", "ok"]));
        Assert.False(OrderSummaryQuery.Totals.NeedsRecount(["wait", "fly", "nd", "ok"]));
    }

    [Fact]
    public void Loc_1_nhom_thi_lay_tong_cua_nhom_do()
    {
        Assert.Equal((4, 2.5m), Totals.For(["wait"]));
        Assert.Equal((13, 20m), Totals.For(["ok"]));
        Assert.False(OrderSummaryQuery.Totals.NeedsRecount(["ok"]));
    }

    [Fact]
    public void Loc_tu_2_nhom_phai_dem_lai_vi_cac_nhom_trung_nhau() =>
        Assert.True(OrderSummaryQuery.Totals.NeedsRecount(["fly", "ok"]));
}
