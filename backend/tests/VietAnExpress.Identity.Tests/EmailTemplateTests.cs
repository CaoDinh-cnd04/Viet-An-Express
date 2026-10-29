using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class EmailTemplateTests
{
    private static readonly EmailCompany Company = new("Việt An Express", "14 Sâm Sơn", "+84 909 805 845", "www.vietanexpress.com.vn", "https://portal.test");

    [Fact]
    public void Thong_bao_co_tieu_de_nhan_quan_trong_doan_van_nut_xem_va_chan_trang()
    {
        var n = Notification.Create("Lịch nghỉ lễ", "Kho nghỉ 2/9.\n\nNhận hàng lại từ 3/9.", true, true, "admin",
            new DateTime(2026, 8, 25, 9, 30, 0), "https://vietanexpress.com.vn/le");

        var mail = NotificationEmail.Email(n, Company);

        Assert.Equal("[Quan trọng] Lịch nghỉ lễ — Việt An Express", mail.Subject);
        var html = mail.HtmlBody!;
        Assert.Contains("Thông báo quan trọng", html);
        Assert.Contains(">Lịch nghỉ lễ</h1>", html);
        Assert.Contains("25/08/2026 09:30", html);
        Assert.Equal(2, html.Split("<p style=\"margin:0 0 14px;").Length - 1); // 2 đoạn văn
        Assert.Contains("href=\"https://vietanexpress.com.vn/le\"", html);
        Assert.Contains("Xem tại đây &rarr;", html);
        Assert.Contains("https://portal.test/notifications", html);
        Assert.Contains("909 805 845", html); // "+" được mã hóa &#x2B; — trình đọc thư vẫn hiện "+84"
        Assert.Contains("Xem tại đây: https://vietanexpress.com.vn/le", mail.Body);
    }

    [Fact]
    public void Ma_hoa_chu_va_bo_link_khong_phai_web()
    {
        var content = new EmailContent(
            "<script>alert(1)</script>",
            [new EmailSection("A & B", "x < y", [new EmailField("Nhãn", "\"giá trị\"")])],
            Button: new EmailLink("Mở", "javascript:alert(1)"));

        var html = EmailTemplate.Html(content, Company with { PortalUrl = null });

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("A &amp; B", html);
        Assert.Contains("x &lt; y", html);
        Assert.Contains("&quot;giá trị&quot;", html);
        Assert.DoesNotContain("javascript:", html);
    }

    [Fact]
    public void Thong_bao_thuong_khong_nhan_va_khong_nut_khi_khong_co_link()
    {
        var mail = NotificationEmail.Email(Notification.Create("Phụ phí", "Tăng 2%.", false, false, "admin", DateTime.Now), Company);

        Assert.Equal("Phụ phí — Việt An Express", mail.Subject);
        Assert.DoesNotContain("Thông báo quan trọng", mail.HtmlBody);
        Assert.DoesNotContain("Xem tại đây", mail.HtmlBody);
    }
}
