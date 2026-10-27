using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Infrastructure.Carriers.HubEz;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Không đơn nào đi HUB-EZ — dùng cho test không liên quan tracking HUB-EZ.</summary>
internal sealed class NoHubEz : IHubEzTracking
{
    public static readonly NoHubEz Instance = new();
    public bool Handles(string? serviceName, string? billConnect) => false;
    public Task<IReadOnlyDictionary<string, HubEzTrack>> TrackAsync(IReadOnlyCollection<string> hawbNumbers, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, HubEzTrack>>(new Dictionary<string, HubEzTrack>());
}

public class HubEzHubsTests
{
    [Theory]
    [InlineData("Chuyên tuyến|EU", "Chuyên tuyến - EU; Chuyên tuyến - UK", true)]
    [InlineData("Chuyên tuyến|UK", "Chuyên tuyến - EU;Chuyên tuyến - UK", true)]
    [InlineData("Chuyên tuyến|USA - Sea", "Chuyên tuyến - USA - Sea", true)]
    [InlineData("chuyên tuyến|eu", "Chuyên tuyến - EU", true)]
    [InlineData("Chuyên tuyến|Thailand - AIR", "Chuyên tuyến - *", true)]
    [InlineData("DHL|Singapore", "Chuyên tuyến - *", false)]
    [InlineData("Chuyên tuyến|USA", "Chuyên tuyến - USA - Sea", false)]
    [InlineData("Chuyên tuyến|EU", "", false)]
    [InlineData(null, "Chuyên tuyến - EU", false)]
    public void Hub_cau_hinh_khop_cot_Dich_Vu(string? service, string rules, bool expected) =>
        Assert.Equal(expected, HubEzHubs.Matches(service, rules));
}

public class HubEzParserTests
{
    private const string Tracking = """
        { "result": [
            { "searchHawbForTrackList": [
                { "desc": "Order Created", "eventTime": "2019-01-07 14:57:10", "statusCode": "C", "locationName": "Hong Kong" },
                { "desc": "Out for Delivery", "eventTime": "2019-01-10 05:40:58", "statusCode": "IT", "locationName": "Australia" },
                { "desc": "Transfer to DEST", "eventTime": "2019-01-09 12:21:57", "statusCode": "IT", "locationName": "Australia" }
              ],
              "hawbNumber": "816004025647", "result": true, "resultMsg": "", "signed": true },
            { "searchHawbForTrackList": [], "hawbNumber": "error_number", "result": false, "resultMsg": "There is no information on this HAWB in the system", "signed": false }
          ],
          "targetUrl": null, "success": true, "error": null, "unAuthorizedRequest": false, "__abp": true }
        """;

    [Fact]
    public void Doc_hanh_trinh_moi_nhat_truoc_va_bo_ma_khong_co()
    {
        using var doc = JsonDocument.Parse(Tracking);
        var track = Assert.Single(HubEzParser.Tracks(doc));
        Assert.Equal("816004025647", track.HawbNumber);
        Assert.True(track.Signed);
        Assert.Equal(["Out for Delivery", "Transfer to DEST", "Order Created"], track.Events.Select(e => e.Description));
        Assert.Equal(new DateTime(2019, 1, 10, 5, 40, 58), track.Events[0].Time);
        Assert.Equal("Australia", track.Events[0].Location);
    }

    [Fact]
    public void Doc_token_va_nhan_biet_token_het_han()
    {
        using var ok = JsonDocument.Parse("""{ "result": "abc.def", "success": true, "error": null, "unAuthorizedRequest": false }""");
        using var fail = JsonDocument.Parse("""{ "result": null, "success": false, "error": { "message": "Invalid user name or password" }, "unAuthorizedRequest": false }""");
        using var expired = JsonDocument.Parse("""{ "result": null, "success": false, "unAuthorizedRequest": true }""");
        Assert.Equal("abc.def", HubEzParser.Token(ok));
        Assert.Null(HubEzParser.Token(fail));
        Assert.True(HubEzParser.IsUnauthorized(expired));
        Assert.False(HubEzParser.IsUnauthorized(ok));
    }

    [Fact]
    public void JSON_la_khong_lam_hong() =>
        Assert.Empty(HubEzParser.Tracks(JsonDocument.Parse("""{ "result": "x" }""")));
}

public class HubEzEventsTests
{
    private static readonly HubEzTrack Track = new("H1",
        [new HubEzEvent(new DateTime(2026, 10, 20, 9, 5, 0), "Arrived at destination", "Germany", "IT")], Signed: false);

    [Fact]
    public void Hanh_trinh_HUB_EZ_nam_giua_POD_va_cac_moc_cu()
    {
        IReadOnlyList<OrderEventDto> legacy =
        [
            new("21/10/2026 10:00", "Đã giao hàng", null),
            new("18/10/2026", "Hàng đã rời kho Việt An Express", null),
            new("17/10/2026", "Đã tạo vận đơn", null)
        ];
        var merged = HubEzEvents.Merge(legacy, Track, hasPod: true);
        Assert.Equal(["Đã giao hàng", "Arrived at destination", "Hàng đã rời kho Việt An Express", "Đã tạo vận đơn"], merged.Select(e => e.Title));
        Assert.Equal("20/10/2026 09:05", merged[1].Time);
        Assert.Equal("Germany", merged[1].Location);
    }

    [Fact]
    public void Chua_giao_thi_HUB_EZ_len_dau_khong_co_HUB_EZ_thi_giu_nguyen()
    {
        IReadOnlyList<OrderEventDto> legacy = [new("17/10/2026", "Đã tạo vận đơn", null)];
        Assert.Equal(["Arrived at destination", "Đã tạo vận đơn"], HubEzEvents.Merge(legacy, Track, hasPod: false).Select(e => e.Title));
        Assert.Same(legacy, HubEzEvents.Merge(legacy, null, hasPod: false));
    }
}

public class HubEzTrackingClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string TrackBody = """
        { "result": [ { "searchHawbForTrackList": [ { "desc": "Order Created", "eventTime": "2026-10-20 08:00:00", "statusCode": "C", "locationName": "VN" } ],
          "hawbNumber": "H1", "result": true, "signed": false } ], "success": true, "unAuthorizedRequest": false }
        """;

    private static HubEzTrackingClient Client(FakeHandler handler, string user = "test", string hubs = "Chuyên tuyến - EU") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://test.hub-ez.com/") },
            Options.Create(new HubEzOptions { UserName = user, Password = "123456", Hubs = hubs }),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<HubEzTrackingClient>.Instance);

    [Fact]
    public async Task Dang_nhap_roi_lay_hanh_trinh_va_luu_dem()
    {
        var paths = new List<string>();
        var handler = new FakeHandler(req =>
        {
            paths.Add(req.RequestUri!.AbsolutePath);
            return req.RequestUri.AbsolutePath.EndsWith("Authenticate", StringComparison.Ordinal)
                ? FakeHandler.Json("""{ "result": "tok", "success": true, "unAuthorizedRequest": false }""")
                : FakeHandler.Json(TrackBody);
        });
        var client = Client(handler);

        var first = await client.TrackAsync(["H1"], Ct);
        var second = await client.TrackAsync(["h1"], Ct);

        Assert.Equal("Order Created", Assert.Single(first["H1"].Events).Description);
        Assert.True(second.ContainsKey("h1"));
        Assert.Equal(["/api/account/Authenticate", "/api/services/app/hawb/ApiGetHawbTracking"], paths); // lần 2 lấy từ bộ đệm
    }

    [Fact]
    public async Task Token_het_han_thi_dang_nhap_lai_mot_lan()
    {
        var logins = 0;
        var tracks = 0;
        var handler = new FakeHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("Authenticate", StringComparison.Ordinal))
                return FakeHandler.Json($$"""{ "result": "tok{{++logins}}", "success": true }""");
            tracks++;
            return req.Headers.Authorization?.Parameter == "tok1"
                ? FakeHandler.Json("""{ "result": null, "success": false, "unAuthorizedRequest": true }""", HttpStatusCode.Unauthorized)
                : FakeHandler.Json(TrackBody);
        });

        var result = await Client(handler).TrackAsync(["H1"], Ct);

        Assert.True(result.ContainsKey("H1"));
        Assert.Equal(2, logins);
        Assert.Equal(2, tracks);
    }

    [Fact]
    public async Task HUB_EZ_loi_thi_tra_rong_khong_nem_loi()
    {
        var result = await Client(new FakeHandler(_ => throw new HttpRequestException("mất mạng"))).TrackAsync(["H1"], Ct);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Chua_cau_hinh_tai_khoan_thi_khong_goi_HUB_EZ()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("không được gọi"));
        var client = Client(handler, user: "");
        Assert.False(client.Handles("Chuyên tuyến|EU", "H1"));
        Assert.Empty(await client.TrackAsync(["H1"], Ct));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Chi_don_thuoc_hub_cau_hinh_va_co_ma_hang()
    {
        var client = Client(new FakeHandler(_ => throw new InvalidOperationException()));
        Assert.True(client.Handles("Chuyên tuyến|EU", "H1"));
        Assert.False(client.Handles("Chuyên tuyến|EU", " "));
        Assert.False(client.Handles("DHL|Singapore", "H1"));
    }
}
