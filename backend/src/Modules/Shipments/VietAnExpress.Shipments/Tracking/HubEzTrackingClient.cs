using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Tracking;

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
