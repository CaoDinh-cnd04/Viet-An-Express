using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Catalog;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class HsLookupTests
{
    [Theory]
    [InlineData("US", new[] { "US" })]
    [InlineData("gb", new[] { "GB", "UK" })]
    [InlineData("UK", new[] { "GB", "UK" })]
    [InlineData("AU", new[] { "AU" })]
    public void Parse_chuan_hoa_nuoc(string country, string[] expected) =>
        Assert.Equal(expected, HsLookupRules.Parse(country, "mango", null)!.Countries);

    [Theory]
    [InlineData("VN", "mango")]   // nước không có biểu thuế riêng
    [InlineData(null, "mango")]
    [InlineData("US", "a")]       // từ khóa quá ngắn
    [InlineData("US", "  ")]
    [InlineData("US", "0.")]      // toàn ký hiệu, chưa đủ 2 số
    public void Parse_tu_choi_dau_vao_khong_tra(string? country, string q) => Assert.Null(HsLookupRules.Parse(country, q, null));

    [Fact]
    public void Parse_tach_tim_theo_ma_va_theo_chu()
    {
        var code = HsLookupRules.Parse("US", "0804.50 00", "catalog")!;
        Assert.Equal("08045000", code.Digits);
        Assert.Empty(code.Terms);
        Assert.False(code.VerifiedOnly);

        var text = HsLookupRules.Parse("US", " dried  mango Dried ", null)!;
        Assert.Null(text.Digits);
        Assert.Equal(["dried", "mango"], text.Terms);
        Assert.True(text.VerifiedOnly); // mặc định chỉ mã đã xác minh
    }

    private static HsCodeEntry Row(long id, string country, string hs6, string national, string en, string? vi = null, bool verified = true, string? desc = null)
    {
        var e = (HsCodeEntry)Activator.CreateInstance(typeof(HsCodeEntry), nonPublic: true)!;
        void Set(string name, object? value) => typeof(HsCodeEntry).GetProperty(name)!.SetValue(e, value);
        Set(nameof(HsCodeEntry.Id), id); Set(nameof(HsCodeEntry.CountryCode), country); Set(nameof(HsCodeEntry.Hs6), hs6);
        Set(nameof(HsCodeEntry.NationalCode), national); Set(nameof(HsCodeEntry.NameEn), en); Set(nameof(HsCodeEntry.NameVi), vi);
        Set(nameof(HsCodeEntry.IsVerified), verified); Set(nameof(HsCodeEntry.NationalDescription), desc);
        return e;
    }

    private static readonly HsCodeEntry[] Rows =
    [
        Row(1, "US", "080450", "0804.50.40.40", "Dried mango", "Xoài sấy", desc: "Guavas, mangoes: dried"),
        Row(2, "US", "080450", "0804504060", "Mango, fresh", "Xoài tươi", verified: false),
        Row(3, "UK", "080450", "0804500000", "Dried mango", "Xoài sấy"),
        Row(4, "US", "610910", "6109100012", "Cotton T-shirt", "Áo thun cotton")
    ];

    private static List<HsLookupItemDto> Run(string country, string q, string? mode = null) =>
        HsLookupRules.Build(Rows.AsQueryable(), HsLookupRules.Parse(country, q, mode)!).ToList();

    [Fact]
    public void Build_loc_theo_nuoc_ma_so_co_dau_cham_va_xac_minh()
    {
        Assert.Equal(["0804.50.40.40"], Run("US", "0804").Select(x => x.NationalCode));
        Assert.Equal(["0804.50.40.40", "0804504060"], Run("US", "080450", "catalog").Select(x => x.NationalCode)); // đã xác minh lên trước
        Assert.Equal(["0804500000"], Run("GB", "0804").Select(x => x.NationalCode)); // GB khớp dữ liệu nạp là UK
    }

    [Fact]
    public void Build_dich_duoc_sang_SQL_Server_tim_chu_bo_dau_va_khong_doc_thue_suat()
    {
        using var db = new ShipmentsDbContext(new DbContextOptionsBuilder<ShipmentsDbContext>()
            .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=True;Encrypt=True").Options);
        var sql = HsLookupRules.Build(db.Set<HsCodeEntry>(), new HsLookupFilter(["GB", "UK"], "0804", ["dried"], false)).ToQueryString();
        Assert.Contains("[dbo].[Catalog_HsCode]", sql);
        Assert.Contains("REPLACE([c].[NationalCode], '?', '')", sql); // mã AU lẫn "?" vẫn tìm được theo số
        // Tìm theo chữ bỏ qua dấu (xoai say → Xoài sấy) trên cả tên Việt, tên Anh và mô tả biểu thuế.
        Assert.Equal(3, sql.Split("COLLATE Latin1_General_100_CI_AI").Length - 1);
        Assert.Contains("TOP", sql);
        Assert.DoesNotContain("[DutyRate]", sql);
    }

    [Theory]
    [InlineData("0804", "0804")]
    [InlineData("080450", "0804.50")]
    [InlineData("61091", "6109.1")]
    [InlineData("6109100012", "6109.10.00.12")]
    public void DottedPrefix_nhom_ma_nhu_bieu_thue(string digits, string expected) => Assert.Equal(expected, HsLookupRules.DottedPrefix(digits));

    private static HsLookupItemDto Raw(string name, string? description, string? unit = null) =>
        new("", name, "6109.10", "6109.10.00.12", description, unit, null, false, []);

    [Fact]
    public void Present_tach_duong_dan_cap_cha_cua_bieu_thue_US()
    {
        var x = HsLookupRules.Present(Raw("Men's (338)",
            "T-shirts, singlets, tank tops and similar garments, knitted or crocheted: > Of cotton > Men's or boys': > Other T-shirts: > Men's (338)"));
        Assert.Equal(["T-shirts, singlets, tank tops and similar garments, knitted or crocheted", "Of cotton", "Men's or boys'", "Other T-shirts"], x!.Path);
        Assert.Equal("Men's (338)", x!.NameEn);
    }

    [Fact]
    public void Present_mo_ta_trung_ten_thi_khong_co_duong_dan() =>
        Assert.Empty(HsLookupRules.Present(Raw("T-shirts", "T-shirts"))!.Path);

    [Fact]
    public void Present_bo_the_HTML_va_ky_tu_ma_hoa()
    {
        var x = HsLookupRules.Present(Raw("Tom &amp; <i>Jerry</i>", null, "m<sup style=\"font-size: 9.75px;\">3</sup>, <u>kg</u>"));
        Assert.Equal("Tom & Jerry", x!.NameEn);
        Assert.Equal("m3, kg", x.Unit);
        Assert.Null(x.NationalDescription);
        Assert.Empty(x.Path);
    }

    [Theory]
    [InlineData("?0805.40.00.26", "0805.40.00.26")]
    [InlineData("?0?303.67.00.65", "0303.67.00.65")]
    [InlineData("0402.2.160", "0402.2")]
    [InlineData("?0305.160", "0305")]
    [InlineData("6109.10.00.12", "6109.10.00.12")]
    [InlineData("0805.21.00.27160", "0805.21.00.27")]
    [InlineData("8423.90.160160160160", "8423.90")]
    [InlineData("4401.1.160160", "4401.1")]
    [InlineData("2711.12.90**.02", "2711.12.90.02")]
    [InlineData("? 0307.49.00.40", "0307.49.00.40")]
    [InlineData("&#160;8703.80.11.52", "8703.80.11.52")]
    [InlineData("0101", "0101")]
    [InlineData("8486.30.40.160811601", null)] // không tách được mã đúng → ẩn
    [InlineData("Fe + Si&#160;&#160; ", null)]
    [InlineData("Fe&#160;&#160; Iron.", null)]
    [InlineData("&#160;", null)]
    public void CleanCode_sua_loi_lay_du_lieu_ma_AU(string raw, string? expected) => Assert.Equal(expected, HsLookupRules.CleanCode(raw));

    [Fact]
    public void Present_AU_lam_sach_ma_don_vi_va_bo_dong_rac()
    {
        var au = HsLookupRules.Present(new("", "Grapefruit and&#160;pomelos", "0805.40", "?0805.40.00.26",
            "CITRUS FRUIT, FRESH OR DRIED: > Grapefruit and&#160;pomelos", "..", null, false, []))!;
        Assert.Equal("0805.40.00.26", au.NationalCode);
        Assert.Equal("Grapefruit and pomelos", au.NameEn);
        Assert.Equal(["CITRUS FRUIT, FRESH OR DRIED"], au.Path);
        Assert.Null(au.Unit); // ".." = không có đơn vị
        Assert.Null(HsLookupRules.Present(new("", "COPPER MATTES", "1601.60", "Ag&#160;&#160; Silve", null, null, null, false, [])));
    }
}
