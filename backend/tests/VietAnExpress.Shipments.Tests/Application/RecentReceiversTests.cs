using VietAnExpress.Shipments.AddressBook;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class RecentReceiversTests
{
    private static RecentReceiverDto R(string company, string? phone, string? addr1, int day, string? city = "SINGAPORE") =>
        new(company, "Ms. KELLY", phone, "+65", null, null, "Singapore", city, null, "349565", addr1, null, null, null, null, new DateOnly(2026, 10, day));

    [Fact]
    public void Moi_nguoi_nhan_1_dong_giu_don_moi_nhat()
    {
        var rows = new[]
        {
            R("SEE SENG PTE LTD", "83682275 - 62933921", "80 GENTING LANE ,", 3),
            R("see seng pte ltd ", "83682275 - 62933921", "80  genting lane ,", 1, city: "OLD"), // trùng (hoa thường, khoảng trắng)
            R("SEE SENG PTE LTD", "99999999", "80 GENTING LANE ,", 2),                           // khác số điện thoại → dòng riêng
            R("   ", "1", "x", 1)                                                                // thiếu tên → bỏ
        };

        var list = RecentReceivers.Distinct(rows, 20);

        Assert.Equal(2, list.Count);
        Assert.Equal(("SINGAPORE", new DateOnly(2026, 10, 3)), (list[0].City, list[0].LastUsed));
        Assert.Equal("99999999", list[1].Phone);
    }

    [Fact]
    public void Cat_khoang_trang_va_gioi_han_so_dong()
    {
        var rows = Enumerable.Range(1, 30).Select(i => R($" CTY {i} ", " 0909 ", " addr ", 1));

        var list = RecentReceivers.Distinct(rows, 20);

        Assert.Equal(20, list.Count);
        Assert.Equal(("CTY 1", "0909", "addr"), (list[0].Company, list[0].Phone, list[0].Address1));
    }

    [Fact]
    public void Don_moi_nhat_trong_ma_buu_chinh_thi_lay_tu_lan_gui_truoc()
    {
        var rows = new[]
        {
            R("SEE SENG PTE LTD", "83682275", "80 GENTING LANE ,", 3) with { PostalCode = null, Email = "" },
            R("SEE SENG PTE LTD", "83682275", "80 GENTING LANE ,", 2) with { PostalCode = "349565", Email = "admin@seeseng.sg" },
            R("SEE SENG PTE LTD", "83682275", "80 GENTING LANE ,", 1) with { PostalCode = "000000" }
        };

        var merged = RecentReceivers.Distinct(rows, 20).Single();

        Assert.Equal(("349565", "admin@seeseng.sg", new DateOnly(2026, 10, 3)), (merged.PostalCode, merged.Email, merged.LastUsed));
    }
}
