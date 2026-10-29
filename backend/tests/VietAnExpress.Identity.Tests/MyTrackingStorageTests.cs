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
            // Theo thứ tự ô (trang hiển thị theo thứ tự ô của hệ thống cũ).
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

    [Fact]
    public async Task Channels_colors_and_website_are_written_to_the_legacy_columns()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        Assert.True((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess);
        f.Db.ChangeTracker.Clear();
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        // Ô cố định theo ứng dụng: CongTinNhan_01 Hotline, _04 WhatsApp; Email cột riêng; CongTruyenThong_01 Facebook, _05 Twitter.
        Assert.Equal("0901234567", page.CongTinNhan01);
        Assert.Null(page.CongTinNhan02);
        Assert.Equal("+84901234567", page.CongTinNhan04);
        Assert.Equal("cs@sgb.vn", page.Email);
        Assert.Equal("https://facebook.com/example", page.CongTruyenThong01);
        Assert.Equal("https://x.com/example", page.CongTruyenThong05);
        Assert.Equal(("#1e6b2c", "#e0412f", "https://sgb.vn"), (page.Color01, page.Color02, page.Website));
        // Đọc lại đủ cấu hình từ các cột.
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse(page.ConfigJson)));
    }

    [Fact]
    public async Task Old_system_rows_are_read_from_the_slot_columns()
    {
        await using var f = new IdentityFixture();
        await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(FullConfigV2())), Ct);
        // Giả lập CS sửa thẳng các ô ở phần mềm cũ.
        var row = f.Db.Entry(await f.Db.MyTrackingPages.SingleAsync(Ct));
        foreach (var (name, value) in new (string, string?)[]
        {
            ("CongTinNhan02", "0909111222"), ("CongTinNhan04", null), ("Email", null),
            ("CongTruyenThong01", null), ("CongTruyenThong05", null), ("CongTruyenThong03", "https://youtube.com/@sgb")
        }) row.Property(name).CurrentValue = value;
        await f.Db.SaveChangesAsync(Ct);
        f.Db.ChangeTracker.Clear();
        var brand = JsonNode.Parse((await f.Db.MyTrackingPages.SingleAsync(Ct)).ConfigJson)!["brand"]!;
        Assert.Equal(@"[{""type"":""hotline"",""value"":""0901234567""},{""type"":""zalo"",""value"":""0909111222""}]", brand["contacts"]!.ToJsonString());
        Assert.Equal(@"[{""type"":""youtube"",""value"":""https://youtube.com/@sgb""}]", brand["socials"]!.ToJsonString());
    }

    [Fact]
    public async Task Saves_legacy_length_fields_and_ordered_banners_in_two_tables()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        var result = await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
        Assert.True(result.IsSuccess);
        f.Db.ChangeTracker.Clear();
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.Equal("SGB EXPRESS HN", page.CompanyName);
        Assert.Equal(2, await f.Db.MyTrackingBanners.CountAsync(Ct));
        Assert.All(page.Banners, b => { Assert.Equal(page.Id, b.MyTrackingId); Assert.Equal(page.CustomerId, b.CustomerId); });
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse(page.ConfigJson)));
        var published = await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("SaigonbayHN"), Ct);
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse(published.Value.Config.Json)));
    }

    [Fact]
    public async Task Uploads_become_files_while_SQL_keeps_legacy_paths_and_readback_urls_can_be_saved_again()
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
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.StartsWith("/Upload/Banner/", page.Background);
        Assert.StartsWith("/Upload/Logo_ShippingMark/", page.Logo);
        Assert.InRange(page.Background!.Length, 1, 250);
        Assert.Equal(page.Background, page.Banners.First().Source);
        var image = f.Images.Find("Banner", Path.GetFileName(page.Background));
        Assert.NotNull(image);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(image.Value.Path, Ct));
        var display = JsonNode.Parse(result.Value.Config!.Value.Json)!;
        Assert.StartsWith("https://portal.test" + MyTrackingImageStore.ApiPath, display["background"]!.GetValue<string>());
        var savedPath = page.Background;
        Assert.True((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(display)), Ct)).IsSuccess);
        Assert.Equal(savedPath, (await f.Db.MyTrackingPages.SingleAsync(Ct)).Background);
        Assert.Null(f.Images.Find("Banner", "../secret.png"));
        Assert.Null(f.Images.Find("../Banner", "test.png"));
    }

    [Theory]
    [InlineData("description")]
    [InlineData("address")]
    [InlineData("linkUrl")]
    [InlineData("src")]
    [InlineData("background")]
    [InlineData("logo")]
    public async Task Values_above_250_do_not_modify_existing_data(string key)
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        Assert.True((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess);
        var before = (await f.Db.MyTrackingPages.SingleAsync(Ct)).ConfigJson;
        var target = key is "address" or "logo" ? config["brand"]!
            : key is "src" or "linkUrl" ? config["images"]![0]! : config;
        target[key] = new string('x', 251);
        Assert.False((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess);
        Assert.Equal(before, (await f.Db.MyTrackingPages.SingleAsync(Ct)).ConfigJson);
    }

    [Fact]
    public async Task Legacy_image_urls_are_stored_as_paths_and_empty_legacy_banner_rows_are_ignored()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        config["background"] = "https://mytracking.top/Upload/Banner/legacy.jpg";
        config["brand"]!["logo"] = "https://mytracking.top/upload/Logo_ShippingMark/logo.png";
        Assert.True((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess);
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.Equal("/Upload/Banner/legacy.jpg", page.Background);
        Assert.Equal("/upload/Logo_ShippingMark/logo.png", page.Logo);
        page.Banners.Add(new MyTrackingBanner(page.CustomerId, 2, new JsonObject()));
        await f.Db.SaveChangesAsync(Ct);
        Assert.Equal(2, JsonNode.Parse(page.ConfigJson)!["images"]!.AsArray().Count);
        Assert.Equal(3, await f.Db.MyTrackingBanners.CountAsync(Ct));
    }

    [Fact]
    public async Task Invalid_uploads_are_rejected_before_writing_the_database()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        config["background"] = "data:image/png;base64,bm90LWFuLWltYWdl";
        Assert.False((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess);
        config["background"] = "data:image/png;base64,invalid";
        Assert.False((await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct)).IsSuccess);
        Assert.Empty(await f.Db.MyTrackingPages.ToListAsync(Ct));
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
        f.Db.ChangeTracker.Clear();
        Assert.Equal("CÔNG TY TNHH SGB EXPRESS", (await f.Db.MyTrackingPages.SingleAsync(Ct)).CompanyName);
        var published = await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("SaigonbayHN"), Ct);
        Assert.Equal("CÔNG TY TNHH SGB EXPRESS", JsonNode.Parse(published.Value.Config.Json)!["brand"]!["companyName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Replacing_and_removing_images_deletes_previous_rows_and_never_leaves_orphans()
    {
        await using var f = new IdentityFixture();
        var config = FullConfigV2();
        await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
        config["images"]!.AsArray().RemoveAt(0);
        f.Db.ChangeTracker.Clear();
        await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("a", (await f.Db.MyTrackingBanners.SingleAsync(Ct)).ImageKey);
        config["images"] = new JsonArray();
        await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.MyTrackingBanners.ToListAsync(Ct));
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse((await f.Db.MyTrackingPages.SingleAsync(Ct)).ConfigJson)));
    }
}
