using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class VaBillCodeTests
{
    [Theory]
    [InlineData("TP.HCM", 6003585, "US", "VAHCM6003585US")]
    [InlineData("Hà Nội", 6003586, "mx", "VAHN6003586MX")]
    [InlineData("Cần Thơ", 12, "AU", "VACT0000012AU")] // luôn 7 số
    public void Ghep_chi_nhanh_so_va_nuoc(string branch, long number, string country, string expected) =>
        Assert.Equal(expected, VaBillCode.Format(branch, number, country));

    [Theory]
    [InlineData(null, "US")]
    [InlineData("Đà Nẵng", "US")] // chi nhánh chưa có tiền tố
    [InlineData("TP.HCM", "")]
    [InlineData("TP.HCM", "United States")]
    public void Thieu_chi_nhanh_hoac_ma_nuoc_thi_khong_co_ma(string? branch, string country) =>
        Assert.Null(VaBillCode.Format(branch, 6003585, country));

    [Theory]
    [InlineData("6003585", 6003585L)]
    [InlineData(" vahcm6003585us ", 6003585L)]
    [InlineData("VAHUE6003585JP", 6003585L)]
    [InlineData("VAHCM600358US", null)]   // thiếu số
    [InlineData("1Z999AA10123456784", null)] // mã hãng
    [InlineData("", null)]
    public void Doc_so_tu_chu_khach_nhap(string input, long? expected) => Assert.Equal(expected, VaBillCode.Number(input));

    [Theory]
    [InlineData("VAHCM6010842NO", "TP.HCM", "NO")]
    [InlineData("VAHN6010841BR", "Hà Nội", "BR")]
    [InlineData("6010093", null, null)]
    [InlineData(null, null, null)]
    public void Doc_chi_nhanh_va_nuoc_tu_ma_da_luu(string? code, string? branch, string? country)
    {
        Assert.Equal(branch, VaBillCode.BranchOf(code));
        Assert.Equal(country, VaBillCode.CountryOf(code));
    }

    [Fact]
    public void Chi_tiet_don_co_chi_nhanh_ma_nuoc_va_VSVX()
    {
        var dto = LegacyOrderView.ToDetailDto(new LegacyOrder
        {
            Id = 1, OrderNumber = 6010842, VaBill = "VAHCM6010842NO", ConsigneeCountry = "Norway", RemoteAreaFedEx = "Fedex (Tier B)"
        }, new DateTime(2026, 10, 9));
        Assert.Equal(("TP.HCM", "NO", "Fedex (Tier B)"), (dto.Branch, dto.Receiver!.CountryCode, dto.RemoteArea));
    }

    [Fact]
    public void Ma_vach_nhan_kien_quet_vao_van_nhan_ra_so_bill()
    {
        Assert.Equal(6177728, VaBillCode.Number("6177728/1"));
        Assert.Equal(6177728, VaBillCode.Number(" 6177728/12 "));
        Assert.Equal("6177728", VaBillCode.StripPiece("6177728/3"));
        Assert.Equal("ABC/1", VaBillCode.StripPiece("ABC/1")); // không phải số bill → giữ nguyên
        Assert.Null(VaBillCode.Number("6177728/"));
    }

    [Fact]
    public void Hien_thi_so_OrderNumber_7_so_ke_ca_khi_co_VA_Bill()
    {
        Assert.Equal("6003585", LegacyOrderView.BillOf(new LegacyOrder { Id = 1, OrderNumber = 6003585, VaBill = "VAHCM6003585US" }));
        Assert.Equal("15", LegacyOrderView.BillOf(new LegacyOrder { Id = 15, VaBill = "VAHN" })); // nháp chưa có số
        Assert.Equal("6003584", LegacyOrderView.BillOf(new LegacyOrder { Id = 2, OrderNumber = 6003584 }));
    }
}
