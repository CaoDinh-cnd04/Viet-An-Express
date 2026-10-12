using VietAnExpress.Shipments.Application.Orders;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class OrderSummaryTotalsTests
{
    // 19 đơn: chờ đi 3 đơn (4 kiện, 2,5 kg), đang bay 6, chưa phát 2, đã phát 7, vượt ngày 1.
    private static readonly OrderSummaryQuery.Totals Totals = new(19, 3, 6, 2, 7, 1, 30, 40.5m,
        4, 2.5m, 9, 12m, 2, 3m, 13, 20m, 2, 3m);

    [Fact]
    public void Khong_loc_hoac_chon_du_trang_thai_thi_lay_tong_chung()
    {
        Assert.Equal((30, 40.5m), Totals.For([]));
        Assert.Equal((30, 40.5m), Totals.For(["wait", "fly", "nd", "ok", "late"]));
    }

    [Fact]
    public void Loc_trang_thai_thi_chi_cong_cac_trang_thai_do()
    {
        Assert.Equal((4, 2.5m), Totals.For(["wait"]));
        Assert.Equal((17, 26m), Totals.For(["ok", "nd", "late"]));
    }
}
