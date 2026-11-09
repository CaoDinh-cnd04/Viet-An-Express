using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>Tra mã HS qua API của trang https://vietanexpress.com/tra-hs-code (plugin WordPress vae-hscode).</summary>
internal interface IHsWebsiteLookup
{
    /// <summary>Lỗi kết nối / website trả lỗi → ném <see cref="HttpRequestException"/> (không giả là "không tìm thấy").</summary>
    Task<IReadOnlyList<HsLookupItemDto>> SearchAsync(string country, string query, bool verifiedOnly, CancellationToken ct);
}

internal sealed class HsWebsiteLookup(HttpClient http, IMemoryCache cache, IOptions<HsLookupOptions> options) : IHsWebsiteLookup
{
    public async Task<IReadOnlyList<HsLookupItemDto>> SearchAsync(string country, string query, bool verifiedOnly, CancellationToken ct)
    {
        var cc = HsWebsiteMapper.WebsiteCountry(country);
        var path = $"{(verifiedOnly ? "products" : "tariff")}?country={Uri.EscapeDataString(cc)}&q={Uri.EscapeDataString(query)}";
        var key = "hs-web:" + path.ToLowerInvariant();
        if (cache.TryGetValue(key, out IReadOnlyList<HsLookupItemDto>? hit) && hit is not null) return hit;

        using var res = await http.GetAsync(path, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = verifiedOnly ? HsWebsiteMapper.Products(doc.RootElement, cc) : HsWebsiteMapper.Tariff(doc.RootElement);

        if (options.Value.CacheMinutes > 0) cache.Set(key, items, TimeSpan.FromMinutes(options.Value.CacheMinutes));
        return items;
    }
}

/// <summary>Đổi JSON của API website sang <see cref="HsLookupItemDto"/> (chưa làm sạch — handler gọi <see cref="HsLookupRules.Present"/>). Hàm thuần — có test.</summary>
internal static class HsWebsiteMapper
{
    /// <summary>Portal gửi GB cho Anh, website dùng UK.</summary>
    public static string WebsiteCountry(string country) =>
        country.Trim().ToUpperInvariant() is "GB" or "UK" ? "UK" : country.Trim().ToUpperInvariant();

    /// <summary>
    /// /products: <c>{ items: [{ name_vn, name_en, hs6, codes: { US: { code, description, unit } | { options: [{ code, description }] } } }] }</c>
    /// — mặt hàng Việt An đã xác minh. Mặt hàng có nhiều mã tùy đặc điểm (<c>options</c>) → mỗi mã 1 dòng; chưa có mã của nước → mã quốc gia null.
    /// </summary>
    public static IReadOnlyList<HsLookupItemDto> Products(JsonElement root, string websiteCountry)
    {
        var result = new List<HsLookupItemDto>();
        foreach (var it in Items(root))
        {
            var nameVi = Str(it, "name_vn") ?? "";
            var nameEn = Str(it, "name_en") ?? "";
            var hs6 = Dotted(Str(it, "hs6")) ?? "";
            JsonElement code = default;
            var hasCode = it.TryGetProperty("codes", out var codes) && codes.ValueKind == JsonValueKind.Object
                && codes.TryGetProperty(websiteCountry, out code) && code.ValueKind == JsonValueKind.Object;

            if (hasCode && code.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in opts.EnumerateArray())
                    result.Add(new HsLookupItemDto(nameVi, nameEn, hs6, Dotted(Str(o, "code")), Str(o, "description"), Str(o, "unit"), null, true, []));
            }
            else if (hasCode)
                result.Add(new HsLookupItemDto(nameVi, nameEn, hs6, Dotted(Str(code, "code")), Str(code, "description"), Str(code, "unit"), null, true, []));
            else
                result.Add(new HsLookupItemDto(nameVi, nameEn, hs6, null, null, null, null, true, []));
        }
        return Limit(result);
    }

    /// <summary>
    /// /tariff: <c>{ items: [{ code, description: "A &gt; B &gt; tên dòng", unit }], source }</c> — dòng biểu thuế.
    /// Tên dòng = cấp cuối của mô tả (như dữ liệu Catalog_HsCode), HS6 = 6 số đầu của mã.
    /// </summary>
    public static IReadOnlyList<HsLookupItemDto> Tariff(JsonElement root)
    {
        var result = new List<HsLookupItemDto>();
        foreach (var it in Items(root))
        {
            var code = Dotted(Str(it, "code"));
            var description = Str(it, "description");
            var name = description?.Split(" > ", StringSplitOptions.TrimEntries)[^1] ?? "";
            var digits = Digits(Str(it, "code"));
            result.Add(new HsLookupItemDto("", name, Dotted(digits.Length >= 6 ? digits[..6] : digits) ?? "", code, description, Str(it, "unit"), null, false, []));
        }
        return Limit(result);
    }

    private static IReadOnlyList<HsLookupItemDto> Limit(List<HsLookupItemDto> rows) => [.. rows.Take(HsLookupRules.MaxResults)];

    private static IEnumerable<JsonElement> Items(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
            : [];

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())?.Trim() is { Length: > 0 } s ? s : null
            : null;

    private static string Digits(string? s) => new([.. (s ?? "").Where(char.IsAsciiDigit)]);

    /// <summary>"6109100040" / "6109.10.00.40" → "6109.10.00.40" (dạng mã của Catalog_HsCode); trống → null.</summary>
    private static string? Dotted(string? code) => Digits(code) is { Length: > 0 } d ? HsLookupRules.DottedPrefix(d) : null;
}
