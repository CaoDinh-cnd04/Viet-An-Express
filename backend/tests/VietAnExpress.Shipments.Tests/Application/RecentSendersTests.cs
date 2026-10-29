using VietAnExpress.Shipments.AddressBook;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class RecentSendersTests
{
    [Fact]
    public void Gop_nguoi_gui_trung_lay_don_moi_nhat_va_bu_o_trong()
    {
        RecentSenderDto[] rows =
        [
            new(" SGB EXPRESS HN ", null, "0698665256", "241 Tân Xuân, HN", null, null, null, new DateOnly(2026, 10, 9)),
            new("sgb express  hn", "Việt AN", "0698665256", "241 tân xuân, hn", "0309142955-004", null, null, new DateOnly(2026, 10, 1)),
            new("SGB EXPRESS HN", "Lan", "0909", "Kho Long Biên", null, null, null, new DateOnly(2026, 9, 1)),
            new("  ", null, null, null, null, null, null, null)
        ];
        var result = RecentSenders.Distinct(rows, 20);

        Assert.Equal(2, result.Count);
        Assert.Equal(("SGB EXPRESS HN", "Việt AN", "0309142955-004", "241 Tân Xuân, HN"), (result[0].Company, result[0].Contact, result[0].TaxId, result[0].Address));
        Assert.Equal("Kho Long Biên", result[1].Address); // cùng tên khác địa chỉ lấy hàng → 2 dòng
    }
}
