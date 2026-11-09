using VietAnExpress.Identity.Shared;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class OtpEmailTests
{
    private static readonly EmailCompany Company = new("Việt An Express", "14 Sâm Sơn", "+84 909 805 845", "www.vietanexpress.com.vn", "https://portal.test");

    [Fact]
    public void Email_otp_co_ma_noi_bat_logo_nhung_thoi_han_va_ban_chu_thuan()
    {
        var m = OtpEmail.Build("khach@congty.vn", "Nguyễn An", "482913", TimeSpan.FromMinutes(5), "Đổi mật khẩu ngay.", Company);

        Assert.Equal("Mã đăng nhập portal Việt An: 482913", m.Subject);
        Assert.Equal(["khach@congty.vn"], m.To);
        // Logo nhúng (cid) từ tài nguyên trong assembly
        var logo = Assert.Single(m.Attachments!);
        Assert.Equal(OtpEmail.LogoCid, logo.ContentId);
        Assert.Equal("image/png", logo.ContentType);
        Assert.True(logo.Data.Length > 100);
        Assert.Contains($"cid:{OtpEmail.LogoCid}", m.HtmlBody);
        Assert.Contains("482913", m.HtmlBody);
        Assert.Contains("5 phút", m.HtmlBody);
        Assert.Contains("Nguyễn An", m.HtmlBody);
        Assert.Contains("không chia sẻ mã này", m.HtmlBody);
        Assert.Contains("Đổi mật khẩu ngay.", m.HtmlBody);
        Assert.Contains("909 805 845", m.HtmlBody);
        Assert.Contains("482913", m.Body);
        Assert.Contains("Đổi mật khẩu ngay.", m.Body);
    }

    [Fact]
    public void Ten_nguoi_nhan_duoc_ma_hoa_html()
    {
        var html = OtpEmail.Html("<b>x</b>", "111111", "5", "Ghi chú", Company, withLogo: false);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.DoesNotContain("cid:", html);
    }
}
