using VietAnExpress.Shipments.Geo;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Khớp VSVX — dữ liệu mẫu lấy đúng các dòng của dbo.VungXauVungXa (hệ thống cũ).</summary>
public class RemoteAreaMatcherTests
{
    private static readonly RemoteAreaRow[] Us =
    [
        new("UPS", null, "10001", "10014", null),
        new("Fedex", null, "10001", "10014", "Tier A"),
        new("Fedex", null, "10100", "10199", "No")
    ];

    [Fact]
    public void My_10001_giong_he_thong_cu_Fedex_Tier_A()
    {
        Assert.Equal([new RemoteAreaHit("Fedex", "Tier A"), new RemoteAreaHit("UPS", null)], RemoteAreaMatcher.Match(Us, "US", "10001", "NEW YORK"));
        Assert.Equal(2, RemoteAreaMatcher.Match(Us, "US", "10001-1234", null).Count); // ZIP+4
    }

    [Theory]
    [InlineData("10015")]
    [InlineData("1000")]
    [InlineData("")]
    public void Ngoai_khoang_hoac_thieu_ma_thi_khong_co(string postal) => Assert.Empty(RemoteAreaMatcher.Match(Us, "US", postal, "New York"));

    [Fact]
    public void Tier_No_thi_khong_ghi_tier() =>
        Assert.Equal([new RemoteAreaHit("Fedex", null)], RemoteAreaMatcher.Match(Us, "US", "10150", null));

    [Theory]
    [InlineData("001-0001", true)] // Nhật: dữ liệu "00100" = 5 số đầu
    [InlineData("0010001", true)]
    [InlineData("0020001", false)]
    public void Nhat_so_phan_dau_ma(string postal, bool hit) =>
        Assert.Equal(hit, RemoteAreaMatcher.Match([new RemoteAreaRow("Fedex", "", "00100", "00100", "Tier B")], "JP", postal, null).Count == 1);

    [Theory]
    [InlineData("V0B 1T7", true)]
    [InlineData("v0b1x0", true)]
    [InlineData("V0B 2D0", false)]
    public void Canada_bo_khoang_trang(string postal, bool hit) =>
        Assert.Equal(hit, RemoteAreaMatcher.Match([new RemoteAreaRow("UPS", null, "V0B1T7", "V0B1T7", null), new RemoteAreaRow("UPS", null, "V0B1W0", "V0B2C0", null)], "CA", postal, null).Count == 1);

    [Theory]
    [InlineData("HS1 2AB", "HS1X", "HS1X", true)]   // X = mọi mã của quận
    [InlineData("HS12AB", "HS1X", "HS1X", true)]    // gõ liền
    [InlineData("HS2 2AB", "HS1X", "HS1X", false)]
    [InlineData("IM9 4AA", "IM", "IM", true)]       // cả vùng
    [InlineData("IV7 8QP", "IV4", "IV11", true)]    // quận 4–11
    [InlineData("IV12 5AA", "IV4", "IV11", false)]
    [InlineData("IV1 3AB", "IV1 3", "IV1 3", true)] // quận 1 khu 3
    [InlineData("IV1 2AB", "IV1 3", "IV1 3", false)]
    public void Anh_so_theo_vung(string postal, string begin, string end, bool hit) =>
        Assert.Equal(hit, RemoteAreaMatcher.PostalInRange("GB", postal.ToUpperInvariant(), begin, end));

    [Fact]
    public void Dong_co_ca_thanh_pho_va_ma_phai_khop_ca_hai()
    {
        RemoteAreaRow[] au = [new("Au-Post", "BUCKETTY", "2250", "2250", null), new("Au-Post", "CALGA", "2250", "2250", null)];
        Assert.Single(RemoteAreaMatcher.Match(au, "AU", "2250", "Bucketty"));
        Assert.Empty(RemoteAreaMatcher.Match(au, "AU", "2250", "Gosford"));
        Assert.Empty(RemoteAreaMatcher.Match(au, "AU", "2250", null));
    }

    [Fact]
    public void Dong_chi_co_thanh_pho_so_khong_dau_khong_phan_biet_hoa_thuong()
    {
        RemoteAreaRow[] al = [new("Fedex", "Durrës", null, null, "Tier B")];
        Assert.Equal([new RemoteAreaHit("Fedex", "Tier B")], RemoteAreaMatcher.Match(al, "AL", "", "  DURRES "));
        Assert.Empty(RemoteAreaMatcher.Match(al, "AL", "", "Tirana"));
    }
}
