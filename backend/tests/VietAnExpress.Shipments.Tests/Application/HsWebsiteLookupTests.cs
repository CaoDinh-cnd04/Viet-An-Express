using System.Text.Json;
using VietAnExpress.Shipments.Catalog;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>JSON mẫu lấy từ API trang https://vietanexpress.com/tra-hs-code (05/11/2026).</summary>
public class HsWebsiteLookupTests
{
    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    [Theory]
    [InlineData("GB", "UK")]
    [InlineData("uk", "UK")]
    [InlineData("us", "US")]
    [InlineData("AU", "AU")]
    public void Anh_gui_website_la_UK(string portal, string website) => Assert.Equal(website, HsWebsiteMapper.WebsiteCountry(portal));

    [Fact]
    public void Products_doi_mat_hang_da_xac_minh()
    {
        var rows = HsWebsiteMapper.Products(Json("""
            {"items":[
              {"name_vn":"Áo thun","name_en":"T-SHIRT (100% COTTON)","hs6":"610910","codes":{"US":{"code":"6109100040","description":"T-shirts, singlets: > Of cotton > Women's (339)","unit":"doz., kg"}}},
              {"name_vn":"Xoài sấy","name_en":"DRIED MANGO","hs6":"080450","codes":{}},
              {"name_vn":"Túi","name_en":"BAG","hs6":"420222","codes":{"US":{"options":[{"code":"4202221500","description":"Of plastics"},{"code":"4202228100","description":"Of cotton"}]}}}
            ]}
            """), "US");

        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.True(r.IsVerified));
        Assert.Equal(("Áo thun", "T-SHIRT (100% COTTON)", "6109.10", "6109.10.00.40", "doz., kg"),
            (rows[0].NameVi, rows[0].NameEn, rows[0].Hs6, rows[0].NationalCode, rows[0].Unit));
        Assert.Null(rows[1].NationalCode); // chưa có mã của nước
        Assert.Equal(["4202.22.15.00", "4202.22.81.00"], rows.Skip(2).Select(r => r.NationalCode));
    }

    [Fact]
    public void Tariff_lay_ten_dong_la_cap_cuoi_cua_mo_ta()
    {
        var rows = HsWebsiteMapper.Tariff(Json("""
            {"items":[{"code":"6109.10.00.04","description":"T-shirts > Of cotton > Men's or boys'","unit":"doz., kg"},{"code":"0804209000","description":"Dates > Figs > Dried","unit":""}],"source":"HTSUS"}
            """));

        Assert.Equal(("Men's or boys'", "6109.10", "6109.10.00.04", false), (rows[0].NameEn, rows[0].Hs6, rows[0].NationalCode, rows[0].IsVerified));
        Assert.Equal("0804.20.90.00", rows[1].NationalCode);

        var shown = HsLookupRules.Present(rows[1])!;
        Assert.Equal("Dried", shown.NameEn);
        Assert.Equal(["Dates", "Figs"], shown.Path);
        Assert.Null(shown.Unit);
    }

    [Fact]
    public void Present_giu_mat_hang_chua_co_ma_nuoc_neu_co_HS6()
    {
        var keep = HsLookupRules.Present(new HsLookupItemDto("Xoài sấy", "DRIED MANGO", "0804.50", null, null, null, null, true, []));
        Assert.Equal("0804.50", keep!.Hs6);
        Assert.Null(keep.NationalCode);
        Assert.Null(HsLookupRules.Present(new HsLookupItemDto("x", "x", "", null, null, null, null, true, [])));
    }

    [Theory]
    [InlineData("""{"items":null}""")]
    [InlineData("""{"message":"error"}""")]
    [InlineData("[]")]
    public void JSON_la_khong_co_dong(string json) => Assert.Empty(HsWebsiteMapper.Tariff(Json(json)));
}
