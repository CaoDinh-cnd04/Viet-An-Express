using VietAnExpress.Identity.Notifications;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class RichTextTests
{
    [Fact]
    public void Giu_dinh_dang_cho_phep_bo_thuoc_tinh_va_the_nguy_hiem()
    {
        var html = RichText.Sanitize(
            """<p onclick="x()">Phụ phí <b>tăng</b> <span style="color: rgb(220, 38, 38); font-size: 18px; position: fixed" class="a">2%</span><script>alert(1)</script></p>"""
            + """<a href="javascript:alert(1)">bấm</a><img src=x onerror=alert(1)><u>gạch</u><i>nghiêng</i>""");

        Assert.Equal(
            """<p>Phụ phí <b>tăng</b> <span style="color: rgb(220, 38, 38); font-size: 18px">2%</span></p>bấm<u>gạch</u><i>nghiêng</i>""", html);
    }

    [Theory]
    [InlineData("""<span style="color: red; background: url(x)">a</span>""", "<span>a</span>")] // chỉ nhận mã màu hex / rgb
    [InlineData("""<span style="font-size: 200px">a</span>""", "<span>a</span>")]
    [InlineData("""<span style="font-weight: 700; text-decoration-line: underline">a</span>""", """<span style="font-weight: bold; text-decoration: underline">a</span>""")]
    [InlineData("""<b>chưa đóng <i>thẻ""", "<b>chưa đóng <i>thẻ</i></b>")]
    [InlineData("""a < b &amp; c & d<br/>e""", "a &lt; b &amp; c &amp; d<br>e")]
    [InlineData("""<style>p{}</style><!-- x --><div>ok</div></span>""", "<div>ok</div>")]
    public void Loc_tung_truong_hop(string input, string expected) => Assert.Equal(expected, RichText.Sanitize(input));

    [Fact]
    public void Nhan_dien_chu_thuong_va_lay_chu_thuan()
    {
        Assert.False(RichText.IsRich("Giá < 2 kg, xuống dòng\nvẫn là chữ thường"));
        Assert.True(RichText.IsRich("<p>a</p>"));
        Assert.Equal("Dòng 1\nDòng 2 & 3", RichText.ToPlainText("<p>Dòng <b>1</b></p><p>Dòng 2 &amp; 3</p>"));
        Assert.Equal("a < b", RichText.ToPlainText(" a < b "));
    }
}
