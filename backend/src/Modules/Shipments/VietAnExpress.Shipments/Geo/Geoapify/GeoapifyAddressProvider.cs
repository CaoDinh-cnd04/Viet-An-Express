using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo.Geoapify;

/// <summary>
/// Cấu hình Geoapify (section "Geoapify") — gợi ý địa chỉ khi khách gõ địa chỉ người nhận.
/// Key miễn phí (3.000 lượt/ngày) đăng ký tại myprojects.geoapify.com; chỉ nằm ở backend
/// (User Secrets khi dev, biến môi trường Geoapify__ApiKey khi chạy thật) — frontend không gọi thẳng Geoapify.
/// </summary>
internal sealed class GeoapifyOptions
{
    public const string Section = "Geoapify";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://api.geoapify.com";

    /// <summary>Để trống thì tắt gợi ý địa chỉ (khách tự nhập như trước).</summary>
    public string ApiKey { get; set; } = "";

    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>Gợi ý địa chỉ qua Geoapify Address Autocomplete (dữ liệu OpenStreetMap), lọc theo nước đến.</summary>
internal sealed class GeoapifyAddressProvider(
    HttpClient http, IOptions<GeoapifyOptions> options, ILogger<GeoapifyAddressProvider> logger) : IAddressSuggestionProvider
{
    public const int Limit = 6;

    public async Task<AddressSuggestionResult> SuggestAsync(string countryCode, string query, CancellationToken cancellationToken)
    {
        var key = options.Value.ApiKey.Trim();
        if (key.Length == 0) return AddressSuggestionResult.Unavailable;

        var url = $"v1/geocode/autocomplete?text={Uri.EscapeDataString(query)}&filter=countrycode:{countryCode.ToLowerInvariant()}" +
                  $"&format=json&lang=en&limit={Limit}&apiKey={Uri.EscapeDataString(key)}";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Geoapify trả HTTP {Status} cho {Country}", (int)response.StatusCode, countryCode);
                return AddressSuggestionResult.Unavailable;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return AddressSuggestionResult.Found(GeoapifyParser.Suggestions(doc, countryCode));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Không gọi được Geoapify cho {Country}", countryCode);
            return AddressSuggestionResult.Unavailable;
        }
    }
}

/// <summary>Đọc JSON Geoapify (format=json) — hàm thuần, có test.</summary>
internal static class GeoapifyParser
{
    /// <summary>Loại kết quả có số nhà / tên đường để điền ô Địa chỉ 1; loại khác (thành phố, mã bưu chính…) chỉ điền thành phố / tỉnh / mã.</summary>
    private static readonly HashSet<string> StreetLevel = ["building", "street", "amenity"];

    /// <summary>
    /// { results: [ { formatted, address_line1, housenumber, street, city, state, state_code, postcode, country_code, result_type } ] }.
    /// Bỏ kết quả khác nước đang chọn và kết quả trùng nhau.
    /// </summary>
    public static IReadOnlyList<AddressSuggestion> Suggestions(JsonDocument doc, string countryCode)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array) return [];
        var cc = countryCode.ToUpperInvariant();
        var list = new List<AddressSuggestion>();
        foreach (var r in results.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Object || !string.Equals(Str(r, "country_code"), cc, StringComparison.OrdinalIgnoreCase)) continue;
            var label = Str(r, "formatted");
            if (label is null) continue;
            var s = new AddressSuggestion(
                label, Address1(r), Str(r, "city") ?? Str(r, "town") ?? Str(r, "village") ?? Str(r, "county"),
                Str(r, "state"), Str(r, "state_code"), Str(r, "postcode"), cc);
            if (!list.Contains(s)) list.Add(s);
        }
        return list;
    }

    private static string Address1(JsonElement r)
    {
        if (!StreetLevel.Contains(Str(r, "result_type") ?? "")) return "";
        var street = Str(r, "street");
        // Địa điểm (cửa hàng, toà nhà có tên): address_line1 là tên địa điểm — ghép số nhà + đường cho đúng địa chỉ giao hàng.
        // Không có số nhà (Nhật: address_line1 là số khối "1-1") → giữ address_line1 rồi thêm đường.
        if (Str(r, "result_type") == "amenity" && street is not null)
        {
            var house = Str(r, "housenumber");
            if (house is null) return Str(r, "address_line1") is { } line1 && line1 != street ? $"{line1}, {street}" : street;
            var label = Str(r, "formatted") ?? "";
            return label.Contains($"{street} {house}", StringComparison.Ordinal) ? $"{street} {house}" : $"{house} {street}";
        }
        return Str(r, "address_line1") ?? street ?? "";
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
}
