using VietAnExpress.Ecommerce.Domain;
using Xunit;

namespace VietAnExpress.Ecommerce.Tests;

public class MarketplaceOrderTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 9, 0, 0);

    private static ImportedOrder Imported(string name = "#1") =>
        new("1", name, MarketplaceRecipient.Empty, 1, null, "USD", 10, null, null, Now);

    [Fact]
    public void Don_da_xoa_khong_bi_dong_bo_ghi_lai()
    {
        var o = MarketplaceOrder.Import(1, null, "tiktok", Imported(), Now);
        Assert.True(o.Delete(Now));
        Assert.False(o.UpdateFrom(Imported("#1-moi"), Now));
        Assert.Equal("#1", o.OrderName);
        Assert.False(o.Delete(Now)); // xóa lần 2 không có tác dụng
    }

    [Fact]
    public void Don_dong_bo_tu_shop_khong_xoa_duoc()
    {
        var o = MarketplaceOrder.Import(1, 3, "tiktok", Imported(), Now); // StoreConnectionId = 3 (shop đã kết nối)
        Assert.True(o.IsSynced);
        Assert.False(o.CanDelete);
        Assert.False(o.Delete(Now));
        Assert.Null(o.DeletedAt);
        Assert.True(MarketplaceOrder.Import(1, null, "tiktok", Imported(), Now).CanDelete); // nhập file thì xóa được
    }

    [Fact]
    public void Nhap_lai_file_khoi_phuc_don_da_xoa()
    {
        var o = MarketplaceOrder.Import(1, null, "tiktok", Imported(), Now);
        o.Edit(MarketplaceRecipient.Empty with { Name = "Taro" }, 1, null, 1, null, null, null, null, null, Now);
        o.Delete(Now);
        Assert.True(o.Restore(Imported("#1-file"), Now));
        Assert.Null(o.DeletedAt);
        Assert.Null(o.EditedAt);
        Assert.Equal("#1-file", o.OrderName);
        Assert.False(o.Restore(Imported(), Now)); // chưa xóa thì không khôi phục
    }

    [Fact]
    public void Xac_nhan_roi_tra_ve_va_dong_bo_khong_ghi_de_don_da_xac_nhan()
    {
        var o = MarketplaceOrder.Import(1, null, "tiktok", Imported(), Now);
        Assert.True(o.Confirm(Now));
        Assert.False(o.Confirm(Now));
        Assert.False(o.UpdateFrom(Imported("#1-moi"), Now));
        Assert.True(o.Unconfirm(Now));
        Assert.Null(o.ConfirmedAt);
        Assert.True(o.UpdateFrom(Imported("#1-moi"), Now));
    }

    [Fact]
    public void Don_da_gui_khong_xoa_duoc()
    {
        var o = MarketplaceOrder.Import(1, null, "tiktok", Imported(), Now);
        o.Confirm(Now);
        Assert.False(o.Delete(Now));
        Assert.Null(o.DeletedAt);
    }

    [Fact]
    public void Don_nhap_tay_vao_thang_don_hang_cua_toi()
    {
        var o = MarketplaceOrder.CreateManual(1, "manual", Imported(), new ManualShipping(null, null, null, null), Now);
        Assert.Equal(Now, o.ConfirmedAt);
    }

    [Fact]
    public void Don_da_sua_khong_bi_dong_bo_ghi_de()
    {
        var o = MarketplaceOrder.Import(1, null, "tiktok", Imported(), Now);
        Assert.True(o.Edit(MarketplaceRecipient.Empty with { Name = "Taro" }, 1, null, 1, null, null, null, null, null, Now));
        Assert.False(o.UpdateFrom(Imported("#1-moi"), Now));
        Assert.Equal("Taro", o.Recipient.Name);
    }
}
