using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Application.Dtos;

namespace VietAnExpress.Shipments.Infrastructure.Carriers.HubEz;

/// <summary>
/// Cấu hình HUB-EZ (section "HubEz") — chỉ lấy hành trình (tracking) theo mã hãng (MaVanDon.Bill_Connect).
/// Tài khoản nằm ở User Secrets khi dev / biến môi trường HubEz__UserName, HubEz__Password khi chạy thật.
/// Thử nghiệm: TrackingBaseUrl = https://test.hub-ez.com (UAT bắt buộc https — http bị chuyển hướng làm POST thành GET),
/// tài khoản test / 123456 (UAT công khai của HUB-EZ). PRD http://APITracking.hub-ez.com chạy http (https của máy này lỗi 500).
/// </summary>
internal sealed class HubEzOptions
{
    public const string Section = "HubEz";

    /// <summary>Máy chủ tracking (PRD tách riêng máy chủ đặt đơn): token và tracking đều lấy ở đây.</summary>
    [Required, Url]
    public string TrackingBaseUrl { get; set; } = "http://APITracking.hub-ez.com";

    /// <summary>Để trống thì tắt tracking HUB-EZ (chỉ hiện hành trình của hệ thống cũ).</summary>
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>
    /// Hub đi HUB-EZ, cách nhau dấu ";" — dạng như ô Hub của portal, vd "Chuyên tuyến - EU; Chuyên tuyến - UK";
    /// "Hãng - *" = mọi hub của hãng. Để trống = không đơn nào lấy tracking HUB-EZ.
    /// </summary>
    public string Hubs { get; set; } = "";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>Giữ kết quả tracking mỗi mã trong bao lâu trước khi hỏi lại HUB-EZ.</summary>
    [Range(1, 1440)]
    public int CacheMinutes { get; set; } = 10;
}

/// <summary>1 sự kiện hành trình HUB-EZ (giờ ghi đúng như HUB-EZ trả về).</summary>
internal sealed record HubEzEvent(DateTime? Time, string Description, string? Location, string? StatusCode);

/// <summary>Hành trình 1 mã HUB-EZ, mới nhất trước; <see cref="Signed"/> = đã giao.</summary>
internal sealed record HubEzTrack(string HawbNumber, IReadOnlyList<HubEzEvent> Events, bool Signed);

internal interface IHubEzTracking
{
    /// <summary>Đơn thuộc hub đi HUB-EZ (theo cấu hình) và có mã hãng để tra.</summary>
    bool Handles(string? serviceName, string? billConnect);

    /// <summary>Hành trình theo mã HUB-EZ; mã không có / HUB-EZ lỗi thì không có trong kết quả (không ném lỗi).</summary>
    Task<IReadOnlyDictionary<string, HubEzTrack>> TrackAsync(IReadOnlyCollection<string> hawbNumbers, CancellationToken cancellationToken);
}

internal sealed class HubEzTrackingClient(
    HttpClient http, IOptions<HubEzOptions> options, IMemoryCache cache, ILogger<HubEzTrackingClient> logger) : IHubEzTracking
{
    /// <summary>HUB-EZ không ghi hạn token; giữ tối đa chừng này, gặp unAuthorizedRequest thì đăng nhập lại.</summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(6);
    private const string TokenKey = "hubez:token";
    /// <summary>Số mã mỗi lần gọi (tra cứu công khai tối đa 10 mã).</summary>
    private const int BatchSize = 30;

    private bool Enabled => options.Value.UserName.Trim().Length > 0 && options.Value.Password.Length > 0;

    public bool Handles(string? serviceName, string? billConnect) =>
        Enabled && !string.IsNullOrWhiteSpace(billConnect) && HubEzHubs.Matches(serviceName, options.Value.Hubs);

    public async Task<IReadOnlyDictionary<string, HubEzTrack>> TrackAsync(IReadOnlyCollection<string> hawbNumbers, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, HubEzTrack>(StringComparer.OrdinalIgnoreCase);
        if (!Enabled) return result;
        var missing = new List<string>();
        foreach (var hawb in hawbNumbers.Select(h => h.Trim()).Where(h => h.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (cache.TryGetValue(TrackKey(hawb), out HubEzTrack? hit) && hit is not null) result[hawb] = hit;
            else missing.Add(hawb);
        }

        foreach (var batch in missing.Chunk(BatchSize))
        {
            var fetched = await FetchAsync(batch, cancellationToken);
            foreach (var track in fetched)
            {
                cache.Set(TrackKey(track.HawbNumber), track, TimeSpan.FromMinutes(options.Value.CacheMinutes));
                result[track.HawbNumber] = track;
            }
        }
        return result;
    }

    private async Task<IReadOnlyList<HubEzTrack>> FetchAsync(string[] hawbs, CancellationToken ct)
    {
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var token = await TokenAsync(forceNew: attempt > 1, ct);
                if (token is null) return [];
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/services/app/hawb/ApiGetHawbTracking") { Content = JsonContent.Create(hawbs) };
                request.Headers.Authorization = new("Bearer", token);
                using var response = await http.SendAsync(request, ct);
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (HubEzParser.IsUnauthorized(doc) || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    cache.Remove(TokenKey);
                    continue; // token hết hạn → đăng nhập lại 1 lần
                }
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("HUB-EZ tracking trả HTTP {Status}", (int)response.StatusCode);
                    return [];
                }
                return HubEzParser.Tracks(doc);
            }
            logger.LogWarning("HUB-EZ từ chối token sau khi đăng nhập lại");
            return [];
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Không gọi được HUB-EZ tracking cho {Count} mã", hawbs.Length);
            return [];
        }
    }

    private async Task<string?> TokenAsync(bool forceNew, CancellationToken ct)
    {
        if (!forceNew && cache.TryGetValue(TokenKey, out string? cached) && !string.IsNullOrEmpty(cached)) return cached;
        var o = options.Value;
        using var response = await http.PostAsJsonAsync("api/account/Authenticate",
            new { UsernameOrEmailAddress = o.UserName.Trim(), o.Password }, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var token = HubEzParser.Token(doc);
        if (token is null)
        {
            logger.LogWarning("Đăng nhập HUB-EZ thất bại (HTTP {Status}) — kiểm tra HubEz:UserName / Password", (int)response.StatusCode);
            return null;
        }
        cache.Set(TokenKey, token, TokenLifetime);
        return token;
    }

    private static string TrackKey(string hawb) => $"hubez:track:{hawb.ToUpperInvariant()}";
}

/// <summary>Hub nào đi HUB-EZ — so cột Dich_Vu ("Chuyên tuyến|EU") với cấu hình dạng ô Hub ("Chuyên tuyến - EU"). Hàm thuần, có test.</summary>
internal static class HubEzHubs
{
    public static bool Matches(string? serviceName, string rules)
    {
        var service = Normalize((serviceName ?? "").Replace("|", " - ", StringComparison.Ordinal));
        if (service.Length == 0) return false;
        foreach (var raw in rules.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var rule = Normalize(raw);
            if (rule.EndsWith(" - *", StringComparison.Ordinal)
                ? service.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(service, rule, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Gộp khoảng trắng thừa ("Chuyên tuyến  -  EU" = "Chuyên tuyến - EU").</summary>
    private static string Normalize(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
}

/// <summary>Đọc JSON HUB-EZ (khung ABP: { result, success, error, unAuthorizedRequest }). Hàm thuần, có test.</summary>
internal static class HubEzParser
{
    private static readonly string[] TimeFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"];

    public static bool IsUnauthorized(JsonDocument doc) =>
        doc.RootElement.ValueKind == JsonValueKind.Object
        && doc.RootElement.TryGetProperty("unAuthorizedRequest", out var u) && u.ValueKind == JsonValueKind.True;

    /// <summary>{ result: "&lt;token&gt;", success: true }.</summary>
    public static string? Token(JsonDocument doc) =>
        doc.RootElement.ValueKind == JsonValueKind.Object
        && doc.RootElement.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True
        && doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String
        && r.GetString() is { Length: > 0 } token ? token : null;

    /// <summary>
    /// { result: [ { hawbNumber, result, resultMsg, signed, searchHawbForTrackList: [ { desc, eventTime, statusCode, locationName } ] } ] }.
    /// Bỏ mã HUB-EZ không có (result = false); sự kiện xếp mới nhất trước.
    /// </summary>
    public static IReadOnlyList<HubEzTrack> Tracks(JsonDocument doc)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("result", out var list)
            || list.ValueKind != JsonValueKind.Array) return [];
        var tracks = new List<HubEzTrack>();
        foreach (var t in list.EnumerateArray())
        {
            if (t.ValueKind != JsonValueKind.Object || Str(t, "hawbNumber") is not { } hawb) continue;
            if (t.TryGetProperty("result", out var found) && found.ValueKind == JsonValueKind.False) continue;
            var events = new List<HubEzEvent>();
            if (t.TryGetProperty("searchHawbForTrackList", out var evs) && evs.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in evs.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object || Str(e, "desc") is not { } desc) continue;
                    events.Add(new HubEzEvent(Time(Str(e, "eventTime")), desc, Str(e, "locationName"), Str(e, "statusCode")));
                }
            }
            var signed = t.TryGetProperty("signed", out var s) && s.ValueKind == JsonValueKind.True;
            tracks.Add(new HubEzTrack(hawb, [.. events.OrderByDescending(e => e.Time ?? DateTime.MinValue)], signed));
        }
        return tracks;
    }

    private static DateTime? Time(string? value) =>
        value is not null && DateTime.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
}

/// <summary>Ghép hành trình HUB-EZ vào hành trình của hệ thống cũ. Hàm thuần, có test.</summary>
internal static class HubEzEvents
{
    /// <summary>
    /// Thứ tự (mới nhất trước): sự kiện giao hàng của hệ thống cũ (POD) nếu có → hành trình HUB-EZ → các mốc còn lại của hệ thống cũ
    /// (rời kho, tạo vận đơn — luôn xảy ra trước khi hàng tới HUB-EZ).
    /// </summary>
    public static IReadOnlyList<OrderEventDto> Merge(IReadOnlyList<OrderEventDto> legacy, HubEzTrack? track, bool hasPod)
    {
        if (track is null || track.Events.Count == 0) return legacy;
        var carrier = track.Events.Select(e => new OrderEventDto(
            e.Time?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "", e.Description, e.Location));
        return hasPod && legacy.Count > 0
            ? [legacy[0], .. carrier, .. legacy.Skip(1)]
            : [.. carrier, .. legacy];
    }
}
