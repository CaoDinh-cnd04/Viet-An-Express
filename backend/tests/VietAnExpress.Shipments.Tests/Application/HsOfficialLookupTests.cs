using System.Text.Json;
using VietAnExpress.Shipments.Catalog;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>JSON mẫu rút gọn từ API hts.usitc.gov và UK Trade Tariff (05/11/2026).</summary>
public class HsOfficialLookupTests
{
    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    [Theory]
    [InlineData("US", "US")]
    [InlineData("gb", "GB")]
    [InlineData("UK", "GB")]
    [InlineData("AU", null)]
    public void Chi_My_va_Anh_co_bieu_thue_chinh_thuc(string country, string? tariff) => Assert.Equal(tariff, HsOfficialMapper.Tariff(country));

    private const string UsExport6109 = """
        [
          {"htsno":"6109","indent":"0","description":"T-shirts, singlets, tank tops and similar garments, knitted or crocheted:","units":[]},
          {"htsno":"6109.10.00","indent":"1","description":"Of cotton","units":[]},
          {"htsno":"","indent":"2","description":"Men's or boys':","units":[]},
          {"htsno":"6109.10.00.04","indent":"3","description":"T-shirts, all white (352)","units":["doz.","kg"]},
          {"htsno":"","indent":"3","description":"Other T-shirts:","units":[]},
          {"htsno":"6109.10.00.12","indent":"4","description":"Men's (338)","units":["doz.","kg"]},
          {"htsno":"","indent":"2","description":"Women's or girls':","units":[]},
          {"htsno":"6109.10.00.37","indent":"3","description":"Underwear (352)","units":["doz.","kg"]},
          {"htsno":"6109.90","indent":"1","description":"Of other textile materials:","units":[]},
          {"htsno":"6109.90.10.07","indent":"2","description":"Men's or boys' (638)","units":["doz.","kg"]},
          {"htsno":"6110","indent":"0","description":"Sweaters, pullovers:","units":[]}
        ]
        """;

    [Fact]
    public void My_dung_duong_dan_theo_indent_ke_ca_dong_cha_khong_danh_so()
    {
        var rows = HsOfficialMapper.UsLeaves(Json(UsExport6109), "610910", []).ToList();

        Assert.Equal(["6109.10.00.04", "6109.10.00.12", "6109.10.00.37"], rows.Select(r => r.NationalCode));
        var men = rows[1];
        Assert.Equal(("Men's (338)", "6109.10", "doz., kg", false), (men.NameEn, men.Hs6, men.Unit, men.IsVerified));
        Assert.Equal("T-shirts, singlets, tank tops and similar garments, knitted or crocheted > Of cotton > Men's or boys' > Other T-shirts > Men's (338)", men.NationalDescription);
        Assert.Equal("Women's or girls' > Underwear (352)", string.Join(" > ", HsLookupRules.Present(rows[2])!.Path.Skip(2).Append(rows[2].NameEn)));
    }

    [Fact]
    public void My_tra_chu_loc_theo_duong_dan_va_ten_dong()
    {
        var underwear = HsOfficialMapper.UsLeaves(Json(UsExport6109), "6109", ["women", "UNDERWEAR"]).ToList();
        Assert.Equal(["6109.10.00.37"], underwear.Select(r => r.NationalCode));
        // Từ khóa nằm ở mô tả nhóm → mọi mã trong nhóm.
        Assert.Equal(4, HsOfficialMapper.UsLeaves(Json(UsExport6109), "6109", ["t-shirts"]).Count());
    }

    [Fact]
    public void My_chi_lay_nhom_co_mo_ta_chua_tu_khoa_bo_chuong_98_99()
    {
        var headings = HsOfficialMapper.UsHeadings(Json("""
            [
              {"htsno":"1212.91.00.00","indent":"2","description":"Sugar beet"},
              {"htsno":"6109.10.00.04","indent":"3","description":"T-shirts, all white"},
              {"htsno":"6109.90.10.07","indent":"2","description":"T-shirts of man-made fibers"},
              {"htsno":"9820.11.12","indent":"1","description":"T-shirts, other than underwear"},
              {"htsno":"6110.20","indent":"1","description":"Sweatshirts and T-shirts"}
            ]
            """), ["t-shirts"]).ToList();
        Assert.Equal(["6109", "6110"], headings);
    }

    [Fact]
    public void Anh_tra_chu_lay_ma_khai_bao_duoc_kem_cap_cha_bo_chuong()
    {
        var rows = HsOfficialMapper.UkSearch(Json("""
            {"data":{"attributes":{"type":"fuzzy_match","goods_nomenclature_match":{"chapters":[],"headings":[],"commodities":[
              {"_source":{"goods_nomenclature_item_id":"6109100010","description":"T-shirts","declarable":true,
                "ancestor_descriptions":["ARTICLES OF APPAREL","T-shirts, singlets and other vests, knitted or crocheted","Of cotton","T-shirts"]}},
              {"_source":{"goods_nomenclature_item_id":"6109100000","description":"Of cotton","declarable":false,
                "ancestor_descriptions":["ARTICLES OF APPAREL","T-shirts, singlets and other vests, knitted or crocheted","Of cotton"]}}
            ]}}}}
            """));

        var t = Assert.Single(rows);
        Assert.Equal(("6109.10.00.10", "6109.10", "T-shirts"), (t.NationalCode, t.Hs6, t.NameEn));
        Assert.Equal(["T-shirts, singlets and other vests, knitted or crocheted", "Of cotton"], HsLookupRules.Present(t)!.Path);
    }

    [Fact]
    public void Xep_hang_dong_co_tu_khoa_o_phan_nhom_len_truoc()
    {
        HsLookupItemDto Row(string code, string description) =>
            new("", description.Split(" > ")[^1], code[..7], code, description, null, null, false, []);
        var heading = "Dates, figs, pineapples, avocados, guavas, mangoes and mangosteens, fresh or dried";
        var ranked = HsOfficialMapper.Rank([
            Row("0804.10.20.10", $"{heading} > Dates > Whole"),
            Row("0804.20.90.00", $"{heading} > Figs > Dried"),
            Row("0804.50.40.40", $"{heading} > Guavas, mangoes and mangosteens > Mangoes > Dried")
        ], ["dried", "mango"]);
        Assert.Equal(["0804.50.40.40", "0804.20.90.00", "0804.10.20.10"], ranked.Select(r => r.NationalCode));
    }

    [Theory]
    [InlineData("""{"data":{"attributes":{"type":"exact_match","entry":{"endpoint":"headings","id":"6109"}}}}""", "6109")]
    [InlineData("""{"data":{"attributes":{"type":"exact_match","entry":{"endpoint":"chapters","id":"61"}}}}""", null)]
    [InlineData("""{"data":{"attributes":{"type":"fuzzy_match","goods_nomenclature_match":{}}}}""", null)]
    public void Anh_khop_dung_nhom_thi_tai_nhom(string json, string? heading) => Assert.Equal(heading, HsOfficialMapper.UkExactMatch(Json(json)));

    [Fact]
    public void Anh_nhom_4_so_dung_duong_dan_theo_parent_sid()
    {
        var doc = Json("""
            {"data":{"attributes":{"goods_nomenclature_item_id":"6109000000","description_plain":"T-shirts, singlets and other vests","declarable":false}},
             "included":[
               {"type":"commodity","attributes":{"goods_nomenclature_sid":42979,"goods_nomenclature_item_id":"6109100000","description_plain":"Of cotton","leaf":false,"parent_sid":null}},
               {"type":"commodity","attributes":{"goods_nomenclature_sid":99290,"goods_nomenclature_item_id":"6109100010","description_plain":"T-shirts","leaf":true,"parent_sid":42979}},
               {"type":"commodity","attributes":{"goods_nomenclature_sid":42983,"goods_nomenclature_item_id":"6109909000","description_plain":"Of other textile materials","leaf":true,"parent_sid":null}},
               {"type":"footnote","attributes":{"code":"TN701"}}
             ]}
            """);

        var all = HsOfficialMapper.UkHeading(doc, "6109");
        Assert.Equal(["6109.10.00.10", "6109.90.90.00"], all.Select(r => r.NationalCode));
        Assert.Equal("T-shirts, singlets and other vests > Of cotton > T-shirts", all[0].NationalDescription);
        Assert.Equal(["6109.90.90.00"], HsOfficialMapper.UkHeading(doc, "61099").Select(r => r.NationalCode));
    }

    [Fact]
    public void Anh_nhom_khai_bao_duoc_luon_khong_co_ma_con()
    {
        var rows = HsOfficialMapper.UkHeading(Json("""
            {"data":{"attributes":{"goods_nomenclature_item_id":"0409000000","description_plain":"Natural honey","declarable":true}},"included":[]}
            """), "0409");
        Assert.Equal(("0409.00.00.00", "Natural honey"), (rows.Single().NationalCode, rows.Single().NameEn));
    }
}
