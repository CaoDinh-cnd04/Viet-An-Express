using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
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

    [Fact]
    public async Task Saves_fields_and_ordered_banners_in_two_tables_and_keeps_long_images_and_extensions()
    {
        await using var f = new IdentityFixture();
        var config = FullConfig();
        var result = await f.MyTracking.Handle(new SaveMyTrackingCommand("test-page", true, JsonSerializer.SerializeToElement(config)), Ct);
        Assert.True(result.IsSuccess);
        f.Db.ChangeTracker.Clear();
        var page = await f.Db.MyTrackingPages.Include(p => p.Banners).SingleAsync(Ct);
        Assert.Equal("Công ty kiểm thử", page.CompanyName);
        Assert.Equal("https://facebook.com/example", page.Facebook);
        Assert.Equal(2, await f.Db.MyTrackingBanners.CountAsync(Ct));
        Assert.All(page.Banners, b => { Assert.Equal(page.Id, b.MyTrackingId); Assert.Equal(page.CustomerId, b.CustomerId); });
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse(page.ConfigJson)));
        Assert.DoesNotContain("base64", page.ExtraConfigJson!);
        Assert.All(page.Banners, b => Assert.DoesNotContain("base64", b.ExtraConfigJson!));
        var published = await f.MyTracking.Handle(new GetPublicMyTrackingQuery("test-page"), Ct);
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse(published.Value.Config.Json)));
    }

    [Fact]
    public async Task Replacing_and_removing_images_deletes_previous_rows_and_never_leaves_orphans()
    {
        await using var f = new IdentityFixture();
        var config = FullConfig();
        await f.MyTracking.Handle(new SaveMyTrackingCommand("test-page", true, JsonSerializer.SerializeToElement(config)), Ct);
        config["images"]!.AsArray().RemoveAt(0);
        f.Db.ChangeTracker.Clear();
        await f.MyTracking.Handle(new SaveMyTrackingCommand("test-page", true, JsonSerializer.SerializeToElement(config)), Ct);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("a", (await f.Db.MyTrackingBanners.SingleAsync(Ct)).ImageKey);
        config["images"] = new JsonArray();
        await f.MyTracking.Handle(new SaveMyTrackingCommand("test-page", false, JsonSerializer.SerializeToElement(config)), Ct);
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.MyTrackingBanners.ToListAsync(Ct));
        Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse((await f.Db.MyTrackingPages.SingleAsync(Ct)).ConfigJson)));
    }

    [Fact]
    public void Unknown_and_non_string_JSON_values_are_preserved()
    {
        const string json = """{"title":42,"background":null,"images":["legacy-value"],"custom":{"keep":true}}""";
        var page = new MyTrackingPage(1, "test", json, false, DateTime.Now);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(page.ConfigJson)));
    }
}
