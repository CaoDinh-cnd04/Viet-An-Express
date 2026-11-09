using System.Net;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Printing;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Mục người gửi của bill A4: đơn hệ thống cũ in đúng thứ tự cột như bill cũ, đơn portal theo thứ tự mới.</summary>
public class BillSenderLinesTests
{
    private static LegacyOrder Order(string? email, string? address) => new()
    {
        SenderName = "=====(VA Bình Dương)", SenderAddress = address, SenderContactName = "LÊ THỊ MỘNG THÚY",
        SenderPhone = "0395673926", SenderEmail = email, SenderPostalCode = "700000"
    };

    /// <summary>Vị trí từng đoạn chữ trong HTML (đã HtmlEncode như trang in) — phải có đủ và đúng thứ tự.</summary>
    private static void InOrder(string html, params string[] parts)
    {
        var at = parts.Select(p => html.IndexOf(p.StartsWith('>') ? p : WebUtility.HtmlEncode(p), StringComparison.Ordinal)).ToList();
        Assert.All(at, i => Assert.True(i >= 0));
        Assert.Equal(at.OrderBy(i => i), at);
    }

    [Theory]
    [InlineData("178 CMT8, khu phố An Thạnh, TP HCM", true)]
    [InlineData("shop@example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Cot_email_khong_phai_email_la_don_he_thong_cu(string? email, bool legacy) =>
        Assert.Equal(legacy, BillA4.LegacySenderLayout(Order(email, "=")));

    [Fact]
    public void Don_cu_in_dung_thu_tu_cot_nhu_bill_cu()
    {
        var html = BillA4.SenderLines(new OrderPrintModel(Order("178 CMT8, khu phố An Thạnh, TP HCM", "="), []), "", "");
        InOrder(html, "=====(VA Bình Dương)", ">=<", "Postal code:", "LÊ THỊ MỘNG THÚY", "0395673926", "178 CMT8, khu phố An Thạnh, TP HCM");
        Assert.Contains("class=\"ln wrap\"", html); // địa chỉ ở cột Email được xuống 2 dòng
    }

    [Fact]
    public void Don_portal_ten_tai_khoan_truoc_roi_nuoc_dia_chi_dien_thoai_lien_he_email()
    {
        var html = BillA4.SenderLines(new OrderPrintModel(Order("shop@example.com", "241 Tân Xuân"), []), "241 Tân Xuân", "Bắc Từ Liêm");
        InOrder(html, "=====(VA Bình Dương)", "Postal code:", "241 Tân Xuân", "Bắc Từ Liêm", "0395673926", "LÊ THỊ MỘNG THÚY", "shop@example.com");

        // Không có email: vẫn đủ 7 dòng, dòng cuối để trống.
        var empty = BillA4.SenderLines(new OrderPrintModel(Order(null, "241 Tân Xuân"), []), "241 Tân Xuân", "");
        Assert.EndsWith("<div class=\"ln\"></div>", empty);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(empty, "<div class=\"ln").Count);
    }
}
