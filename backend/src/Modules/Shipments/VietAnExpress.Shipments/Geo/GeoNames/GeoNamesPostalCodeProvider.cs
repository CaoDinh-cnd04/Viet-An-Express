using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo.GeoNames;

/// <summary>
/// Tra mã bưu chính qua GeoNames <c>postalCodeLookupJSON</c> (~100 nước, nhận cả mã đầy đủ như "SW1A 1AA", "H3Z 2Y7").
/// HttpClient có BaseAddress / timeout lấy từ <see cref="GeoNamesOptions"/> (đăng ký ở ShipmentsModule).
/// </summary>
internal sealed class GeoNamesPostalCodeProvider(
    HttpClient http, IOptions<GeoNamesOptions> options, ILogger<GeoNamesPostalCodeProvider> logger) : IPostalCodeProvider
{
    public const string LookupPath = "postalCodeLookupJSON";
    public const string SearchPath = "postalCodeSearchJSON";

    public async Task<PostalSearchResult> SearchAsync(string countryCode, string prefix, CancellationToken cancellationToken)
    {
        var username = options.Value.Username.Trim();
        if (username.Length == 0) return PostalSearchResult.Unavailable;

        var url = $"{SearchPath}?postalcode_startsWith={Uri.EscapeDataString(prefix)}&country={countryCode}&maxRows={GeoNamesParser.MaxSuggestions}&username={Uri.EscapeDataString(username)}";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GeoNames trả HTTP {Status} khi gợi ý {Country}/{Prefix}", (int)response.StatusCode, countryCode, prefix);
                return PostalSearchResult.Unavailable;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (GeoNamesParser.Error(doc) is { } error)
            {
                logger.LogWarning("GeoNames lỗi khi gợi ý {Country}/{Prefix}: {Error}", countryCode, prefix, error);
                return PostalSearchResult.Unavailable;
            }
            return new PostalSearchResult(true, GeoNamesParser.Suggestions(doc, countryCode));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Không gọi được GeoNames khi gợi ý {Country}/{Prefix}", countryCode, prefix);
            return PostalSearchResult.Unavailable;
        }
    }

    public async Task<PostalLookupResult> LookupAsync(string countryCode, string postalCode, CancellationToken cancellationToken)
    {
        var username = options.Value.Username.Trim();
        if (username.Length == 0)
        {
            logger.LogWarning("Chưa cấu hình GeoNames:Username — bỏ qua tra mã bưu chính");
            return PostalLookupResult.Unavailable;
        }

        var url = $"{LookupPath}?postalcode={Uri.EscapeDataString(postalCode)}&country={countryCode}&maxRows={GeoNamesParser.MaxRows}&username={Uri.EscapeDataString(username)}";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GeoNames trả HTTP {Status} cho {Country}/{Postal}", (int)response.StatusCode, countryCode, postalCode);
                return PostalLookupResult.Unavailable;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            // Lỗi tài khoản / hết lượt trong ngày: HTTP 200 + { status: { message, value } }.
            if (GeoNamesParser.Error(doc) is { } error)
            {
                logger.LogWarning("GeoNames lỗi cho {Country}/{Postal}: {Error}", countryCode, postalCode, error);
                return PostalLookupResult.Unavailable;
            }
            return GeoNamesParser.Postal(doc, countryCode, postalCode) is { } info
                ? PostalLookupResult.Found(info)
                : PostalLookupResult.NotFound;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Không gọi được GeoNames cho {Country}/{Postal}", countryCode, postalCode);
            return PostalLookupResult.Unavailable;
        }
    }
}

/// <summary>Đọc JSON của GeoNames — hàm thuần, có test.</summary>
internal static class GeoNamesParser
{
    /// <summary>Số dòng hỏi GeoNames cho 1 mã (1 mã có thể gồm nhiều khu / nhiều thành phố).</summary>
    public const int MaxRows = 30;

    /// <summary>Số nơi tối đa trả cho form.</summary>
    public const int MaxPlaces = 20;

    /// <summary>Số dòng gợi ý khi khách đang gõ mã.</summary>
    public const int MaxSuggestions = 15;

    /// <summary>postalCodeSearchJSON: { postalCodes: [ { postalCode, placeName, adminName1… } ] } → gợi ý (bỏ dòng trùng, giữ thứ tự).</summary>
    public static IReadOnlyList<PostalSuggestion> Suggestions(JsonDocument doc, string countryCode)
    {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return [];
        if (!root.TryGetProperty("postalCodes", out var codes) && !root.TryGetProperty("postalcodes", out codes)) return [];
        if (codes.ValueKind != JsonValueKind.Array) return [];

        var list = new List<PostalSuggestion>();
        foreach (var row in codes.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) continue;
            var postal = Str(row, "postalCode") ?? Str(row, "postalcode");
            if (postal is null || Place(row, countryCode) is not { } p) continue;
            var item = new PostalSuggestion(postal, p.City, p.State, p.StateCode, p.Area);
            if (!list.Contains(item)) list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// Nước mà placeName của GeoNames là khu phố / bưu cục (nhỏ hơn thành phố): thành phố lấy ở adminName3 (hoặc adminName2),
    /// placeName thành "khu vực". Vd MX 16090 → thành phố "Xochimilco" (adminName2), bang "Distrito Federal" (adminName1), khu vực "Barrio San Pedro" (placeName); IN 110001 → New Delhi + "Connaught Place, Central Delhi".
    /// </summary>
    private static readonly HashSet<string> SubCityPlaceCountries = ["MX", "IN"];

    /// <summary>
    /// postalCodeLookupJSON: { postalcodes: [ { placeName: "New York", adminName1: "New York", adminCode1: "NY", adminName2, adminName3 } ] }.
    /// (postalCodeSearchJSON dùng khoá "postalCodes" — đọc được cả hai.) Gộp các dòng trùng thành danh sách nơi khác nhau.
    /// </summary>
    public static PostalInfo? Postal(JsonDocument doc, string countryCode, string postalCode)
    {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("postalcodes", out var codes) && !root.TryGetProperty("postalCodes", out codes)) return null;
        if (codes.ValueKind != JsonValueKind.Array) return null;

        var places = new List<PostalPlace>();
        foreach (var row in codes.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || Place(row, countryCode) is not { } place || places.Contains(place)) continue;
            places.Add(place);
            if (places.Count == MaxPlaces) break;
        }
        if (places.Count == 0) return null;
        var first = places[0];
        return new PostalInfo(countryCode, postalCode, first.City, first.State, first.StateCode) { Places = places };
    }

    private static PostalPlace? Place(JsonElement row, string countryCode)
    {
        if (Str(row, "placeName") is not { } placeName) return null;
        // Lấy nguyên các trường GeoNames, không tự đổi tên.
        var state = Str(row, "adminName1");
        if (!SubCityPlaceCountries.Contains(countryCode)) return new PostalPlace(placeName, state, Str(row, "adminCode1"), null);
        // Mexico: thành phố = municipio / alcaldía (adminName2) như các hãng vận chuyển dùng; Ấn Độ: adminName3 (vd New Delhi).
        var city = countryCode == "MX"
            ? Str(row, "adminName2") ?? Str(row, "adminName3") ?? placeName
            : Str(row, "adminName3") ?? Str(row, "adminName2") ?? placeName;
        var area = string.Equals(city, placeName, StringComparison.OrdinalIgnoreCase) ? null : placeName;
        return new PostalPlace(city, state, Str(row, "adminCode1"), area);
    }

    /// <summary>Thông báo lỗi ({ status: { message, value } }); null nếu không lỗi.</summary>
    public static string? Error(JsonDocument doc) =>
        doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("status", out var status)
            ? Str(status, "message") ?? "lỗi không rõ"
            : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
}
