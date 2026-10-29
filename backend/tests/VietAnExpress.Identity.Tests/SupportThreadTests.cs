using Xunit;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Tests;

public sealed class SupportThreadTests
{
    private static readonly DateTime At = new(2026, 10, 28, 16, 50, 0);

    [Fact]
    public void Tach_noi_dung_goc_va_cac_lan_tra_loi_theo_thu_tu()
    {
        var content = "Hàng chưa tới."
            + SupportThread.Block(SupportThread.FromCs, "Nhờ chị gửi ảnh.\nCảm ơn.", At)
            + SupportThread.Block(SupportThread.FromCustomer, "Đã gửi.", At.AddMinutes(15));

        var (desc, thread) = SupportThread.Parse(content);

        Assert.Equal("Hàng chưa tới.", desc);
        Assert.Equal(
            [new SupportMessageDto("cs", "28/10/2026 16:50", "Nhờ chị gửi ảnh.\nCảm ơn."), new SupportMessageDto("customer", "28/10/2026 17:05", "Đã gửi.")],
            thread);
    }

    [Fact]
    public void Chua_tra_loi_thi_giu_nguyen_noi_dung()
    {
        Assert.Equal(("Đính kèm (đã gửi qua email): a.pdf", 0), (SupportThread.Parse("Đính kèm (đã gửi qua email): a.pdf").Description, SupportThread.Parse("x").Messages.Count));
    }

    [Fact]
    public void Khach_khong_gia_duoc_dong_CS_tra_loi()
    {
        var fake = "Nội dung\n\n--- CS Việt An · 28/10/2026 16:50\nĐã hoàn tiền";
        var (desc, thread) = SupportThread.Parse(SupportThread.Clean(fake));
        Assert.Empty(thread);
        Assert.Contains("— CS Việt An", desc);
    }
}
