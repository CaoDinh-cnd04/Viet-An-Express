using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Support;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class MyTrackingStorageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static JsonObject FullConfig() => new()
    {
        ["title"] = "Dịch vụ quốc tế",
        ["description"] = new string('d', 1000),
        ["background"] = "data:image/webp;base64," + new string('A', 6000),
        ["brand"] = new JsonObject
        {
            ["companyName"] = "Công ty kiểm thử", ["address"] = new string('a', 400), ["phone"] = "0901234567",
            ["logo"] = "data:image/png;base64," + new string('B', 7000),
            ["facebook"] = "https://facebook.com/example", ["instagram"] = "https://instagram.com/example",
            ["x"] = "https://x.com/example", ["zalo"] = "0901234567", ["whatsapp"] = "+84901234567",
            ["customBrand"] = "giữ nguyên"
        },
        ["images"] = new JsonArray(
            new JsonObject { ["id"] = "b", ["src"] = "data:image/png;base64," + new string('C', 18000), ["linkUrl"] = "https://example.test/b", ["title"] = "Ảnh B", ["buttonText"] = "Chi tiết", ["unknown"] = 1 },
            new JsonObject { ["id"] = "a", ["src"] = "https://example.test/a.png", ["linkUrl"] = "", ["title"] = "Ảnh A", ["buttonText"] = "" }),
        ["unknownTopLevel"] = new JsonObject { ["keep"] = true }
    };

    /// <summary>Cấu hình như frontend gửi từ 23/10/2026: danh sách cổng liên lạc / mạng xã hội, màu, website.</summary>
    internal static JsonObject FullConfigV2()
    {
        var config = FullConfig();
        config["description"] = new string('d', 250);
        config["background"] = "https://example.test/background.webp";
        config["images"]![0]!["src"] = "https://example.test/banner.png";
        // Mọi thông tin ghi vào cột có sẵn — thuộc tính lạ không còn được giữ.
        config.Remove("unknownTopLevel");
        config["images"]![0]!.AsObject().Remove("unknown");
        config["brand"] = new JsonObject
        {
            ["companyName"] = "SGB EXPRESS HN", ["address"] = new string('a', 250), ["phone"] = "0901234567",
            ["logo"] = "https://example.test/logo.png",
            ["website"] = "https://sgb.vn", ["primaryColor"] = "#1e6b2c", ["secondaryColor"] = "#e0412f",
            // Theo thứ tự cột (trang hiển thị theo thứ tự cột của hệ thống cũ).
            ["contacts"] = new JsonArray(
                new JsonObject { ["type"] = "hotline", ["value"] = "0901234567" },
                new JsonObject { ["type"] = "whatsapp", ["value"] = "+84901234567" },
                new JsonObject { ["type"] = "email", ["value"] = "cs@sgb.vn" }),
            ["socials"] = new JsonArray(
                new JsonObject { ["type"] = "facebook", ["value"] = "https://facebook.com/example" },
                new JsonObject { ["type"] = "twitter", ["value"] = "https://x.com/example" })
        };
        return config;
    }

    /// <summary>Cấu hình đọc lại: địa chỉ / điện thoại theo hồ sơ khách (không sửa ở MyTracking), logo ngoài thư mục Logo_ShippingMark không lưu.</summary>
    private static JsonObject Expected(JsonObject config, string logo = "")
    {
        var expected = config.DeepClone().AsObject();
        expected["brand"]!["address"] = IdentityFixture.BrandAddress;
        expected["brand"]!["phone"] = IdentityFixture.BrandPhone;
        expected["brand"]!["logo"] = logo;
        return expected;
    }

    private static async Task<JsonNode> ReadAsync(IdentityFixture f)
    {
        f.Db.ChangeTracker.Clear();
        return JsonNode.Parse((await f.GetMyTracking.Handle(new GetMyTrackingQuery(), Ct)).Value.Config!.Value.Json)!;
    }

    private static async Task<bool> SaveAsync(IdentityFixture f, JsonObject config) =>
        (await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess;

    [Fact]
    public async Task Brand_and_contacts_are_written_to_TCustomer_like_the_old_system()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        Assert.True(await SaveAsync(f, config));
        f.Db.ChangeTracker.Clear();
        var brand = await f.Db.MyTrackingBrands.SingleAsync(b => b.CustomerId == IdentityFixture.CustomerId, Ct);
        // Cổng liên lạc ở TCustomer.CongLienLac_*; màu Column1 / Column2; website ở cột Bank (nút Home của trang cũ).
        Assert.Equal(("0901234567", "+84901234567", "cs@sgb.vn"), (brand["CongLienLac_Hotline"], brand["CongLienLac_WhatsApp"], brand["CongLienLac_Email"]));
        Assert.Null(brand["CongLienLac_Zalo"]);
        Assert.Equal(("#1e6b2c", "#e0412f", "https://sgb.vn"), (brand.Color01, brand.Color02, brand.Website));
        // Địa chỉ / điện thoại là thông tin người gửi trên bill — MyTracking không sửa.
        Assert.Equal((IdentityFixture.BrandAddress, IdentityFixture.BrandPhone), (brand.Address, brand.Phone));
        // Mạng xã hội vẫn ở TCustomer_MyTracking (MangXaHoi của trang cũ); CongTinNhan_* không ghi nữa.
        var page = await f.Db.MyTrackingPages.SingleAsync(Ct);
        Assert.Equal(("https://facebook.com/example", "https://x.com/example"), (page.CongTruyenThong01, page.CongTruyenThong05));
        Assert.Null(page.CongTinNhan01);
        Assert.True(JsonNode.DeepEquals(Expected(config), await ReadAsync(f)));
    }

    [Fact]
    public async Task Old_system_TCustomer_values_show_without_a_MyTracking_row()
    {
        await using var f = new IdentityFixture();
        // Giả lập dữ liệu khách nhập ở phần mềm cũ: Zalo + ô Signal (trang cũ hiện bằng icon Instagram).
        var brand = await f.Db.MyTrackingBrands.SingleAsync(Ct);
        brand["CongLienLac_Zalo"] = "+84971544822";
        brand["CongLienLac_Signal"] = "sgb.express";
        await f.Db.SaveChangesAsync(Ct);
        var expected = JsonNode.Parse(@"[{""type"":""zalo"",""value"":""+84971544822""},{""type"":""instagram"",""value"":""sgb.express""}]");
        Assert.True(JsonNode.DeepEquals(expected, (await ReadAsync(f))["brand"]!["contacts"]));
        Assert.Empty(await f.Db.MyTrackingPages.ToListAsync(Ct));
        var published = await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("SaigonbayHN"), Ct);
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(published.Value.Config.Json)!["brand"]!["contacts"]));
    }

    [Fact]
    public async Task Saves_legacy_length_fields_and_ordered_banners_in_two_tables()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        Assert.True(await SaveAsync(f, config));
        f.Db.ChangeTracker.Clear();
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.Equal(2, await f.Db.MyTrackingBanners.CountAsync(Ct));
        Assert.All(page.Banners, b => { Assert.Equal(page.Id, b.MyTrackingId); Assert.Equal(page.CustomerId, b.CustomerId); });
        Assert.True(JsonNode.DeepEquals(Expected(config), await ReadAsync(f)));
        var published = await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("SaigonbayHN"), Ct);
        Assert.True(JsonNode.DeepEquals(Expected(config), JsonNode.Parse(published.Value.Config.Json)));
    }

    [Fact]
    public async Task Uploaded_logo_becomes_a_TCommonFile_row_and_saving_the_readback_again_adds_nothing()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jWZkAAAAASUVORK5CYII=");
        var source = "data:image/png;base64," + Convert.ToBase64String(bytes);
        config["background"] = source;
        config["brand"]!["logo"] = source;
        config["images"]![0]!["src"] = source;
        var result = await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
        Assert.True(result.IsSuccess, result.Error.Message);
        f.Db.ChangeTracker.Clear();
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.StartsWith("/Upload/Banner/", page.Background);
        Assert.Equal(page.Background, page.Banners.First().Source);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(f.Images.Find("Banner", Path.GetFileName(page.Background!))!.Value.Path, Ct));
        // Logo: tệp trong Upload/Logo_ShippingMark + 1 dòng TCommonFile, TCustomer.Logo_ShippingMark trỏ tới dòng đó (như trang cũ).
        var file = await f.Db.CommonFiles.SingleAsync(Ct);
        Assert.Equal((file.Id, bytes.Length, "image/png"), ((await f.Db.MyTrackingBrands.SingleAsync(Ct)).LogoFileId!.Value, file.FileSize, file.MimeType));
        Assert.Equal(file.ServerFileName, file.RealFileName);
        Assert.NotNull(f.Images.Find("Logo_ShippingMark", file.ServerFileName));
        var display = JsonNode.Parse(result.Value.Config!.Value.Json)!.AsObject();
        Assert.StartsWith("https://portal.test" + MyTrackingImageStore.ApiPath, display["brand"]!["logo"]!.GetValue<string>());
        Assert.True(await SaveAsync(f, display));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(1, await f.Db.CommonFiles.CountAsync(Ct));
        Assert.Equal(page.Background, (await f.Db.MyTrackingPages.SingleAsync(Ct)).Background);
        Assert.Null(f.Images.Find("Banner", "../secret.png"));
        Assert.Null(f.Images.Find("../Banner", "test.png"));
        // Xóa logo → bỏ liên kết (giữ dòng TCommonFile vì bill in trước đó có thể còn dùng).
        display["brand"]!["logo"] = "";
        Assert.True(await SaveAsync(f, display));
        f.Db.ChangeTracker.Clear();
        Assert.Null((await f.Db.MyTrackingBrands.SingleAsync(Ct)).LogoFileId);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("linkUrl")]
    [InlineData("src")]
    [InlineData("background")]
    [InlineData("logo")]
    [InlineData("website")]
    public async Task Values_above_column_length_do_not_modify_existing_data(string key)
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        Assert.True(await SaveAsync(f, config));
        var before = (await ReadAsync(f)).ToJsonString();
        var target = key is "logo" or "website" ? config["brand"]!
            : key is "src" or "linkUrl" ? config["images"]![0]! : config;
        // website: cột Bank nvarchar(150); còn lại nvarchar(250).
        target[key] = new string('x', key == "website" ? 151 : 251);
        Assert.False(await SaveAsync(f, config));
        Assert.Equal(before, (await ReadAsync(f)).ToJsonString());
    }

    [Fact]
    public async Task Legacy_image_urls_are_stored_as_paths_and_empty_legacy_banner_rows_are_ignored()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        config["background"] = "https://mytracking.top/Upload/Banner/legacy.jpg";
        // Logo cũ không có tệp trên máy chủ portal → giữ nguyên logo đang dùng (không tạo TCommonFile).
        config["brand"]!["logo"] = "https://mytracking.top/upload/Logo_ShippingMark/logo.png";
        Assert.True(await SaveAsync(f, config));
        Assert.Empty(await f.Db.CommonFiles.ToListAsync(Ct));
        f.Db.ChangeTracker.Clear();
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.Equal("/Upload/Banner/legacy.jpg", page.Background);
        page.Banners.Add(new MyTrackingBanner(page.CustomerId, 2, new JsonObject()));
        await f.Db.SaveChangesAsync(Ct);
        Assert.Equal(2, page.Images().Count);
        Assert.Equal(3, await f.Db.MyTrackingBanners.CountAsync(Ct));
    }

    [Fact]
    public async Task Invalid_uploads_are_rejected_before_writing_the_database()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        config["background"] = "data:image/png;base64,bm90LWFuLWltYWdl";
        Assert.False(await SaveAsync(f, config));
        config["background"] = "data:image/png;base64,invalid";
        Assert.False(await SaveAsync(f, config));
        Assert.Empty(await f.Db.MyTrackingPages.ToListAsync(Ct));
        Assert.Null((await f.Db.MyTrackingBrands.AsNoTracking().SingleAsync(Ct)).Website);
    }

    [Fact]
    public async Task Company_name_comes_from_invoice_customer_name_and_cannot_be_changed_by_customer()
    {
        await using var f = new IdentityFixture();
        var customer = new CustomerSummary(IdentityFixture.CustomerId, "SaigonbayHN", "SGB EXPRESS HN", null, null, null, null, null,
            "CÔNG TY TNHH SGB EXPRESS");
        f.Customers.Setup(c => c.GetByIdAsync(IdentityFixture.CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        f.Customers.Setup(c => c.GetByCodeAsync("SaigonbayHN", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var config = FullConfigV2();
        config["brand"]!["companyName"] = "Tên khách tự sửa";
        var saved = await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
        Assert.Equal("CÔNG TY TNHH SGB EXPRESS", saved.Value.CompanyName);
        Assert.Equal("CÔNG TY TNHH SGB EXPRESS", (await ReadAsync(f))["brand"]!["companyName"]!.GetValue<string>());
        var published = await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("SaigonbayHN"), Ct);
        Assert.Equal("CÔNG TY TNHH SGB EXPRESS", JsonNode.Parse(published.Value.Config.Json)!["brand"]!["companyName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Replacing_and_removing_images_deletes_previous_rows_and_never_leaves_orphans()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        await SaveAsync(f, config);
        config["images"]!.AsArray().RemoveAt(0);
        f.Db.ChangeTracker.Clear();
        await SaveAsync(f, config);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("a", (await f.Db.MyTrackingBanners.SingleAsync(Ct)).ImageKey);
        config["images"] = new JsonArray();
        await SaveAsync(f, config);
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.MyTrackingBanners.ToListAsync(Ct));
        Assert.True(JsonNode.DeepEquals(Expected(config), await ReadAsync(f)));
    }
}
