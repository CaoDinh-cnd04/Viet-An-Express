using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace VietAnExpress.Shipments.Geo;

/// <summary>Tra cứu địa lý cho form tạo đơn / import — điểm vào duy nhất mà API và các handler dùng.</summary>
internal interface IGeoLookup
{
    Task<IReadOnlyList<CountryInfo>> GetCountriesAsync(CancellationToken cancellationToken);

    /// <summary>Null khi không tìm thấy, nước chưa được hỗ trợ hoặc nguồn tạm lỗi.</summary>
    Task<PostalInfo?> LookupPostalAsync(string countryCode, string postalCode, CancellationToken cancellationToken);

    /// <summary>
    /// Gợi ý mã bưu chính khi khách đang gõ: mã bắt đầu bằng <paramref name="prefix"/> (≥ 2 ký tự). Không có mã nào bắt đầu như vậy
    /// (vd Anh gõ đủ "SW1A 1AA" mà nguồn chỉ có "SW1A") → tra đúng mã như <see cref="LookupPostalAsync"/>. Rỗng khi nguồn tạm lỗi.
    /// </summary>
    Task<IReadOnlyList<PostalSuggestion>> SearchPostalAsync(string countryCode, string prefix, CancellationToken cancellationToken);

    /// <summary>Gợi ý địa chỉ trong 1 nước theo chữ khách gõ. Rỗng khi chữ quá ngắn, chưa cấu hình hoặc nguồn tạm lỗi.</summary>
    Task<IReadOnlyList<AddressSuggestion>> SuggestAddressesAsync(string countryCode, string query, CancellationToken cancellationToken);
}

/// <summary>
/// Điều phối tra cứu địa lý:
/// - Danh sách nước + mã điện thoại: bộ dữ liệu world-countries (nguồn gốc của REST Countries) qua CDN jsDelivr — cache 1 ngày.
///   (REST Countries v3.1 đã ngừng, v5 bắt buộc API key.) Ghim phiên bản để dữ liệu không tự đổi.
/// - Mã bưu chính: chuẩn hóa + kiểm tra đầu vào, cache, rồi hỏi <see cref="IPostalCodeProvider"/> (hiện là GeoNames);
///   mã đầy đủ không có dữ liệu thì thử phần đầu của mã. Có kết quả cache 7 ngày, không có cache 6 giờ, nguồn lỗi không cache.
/// - Gợi ý mã bưu chính khi gõ: <see cref="IPostalCodeProvider.SearchAsync"/>, cache 1 ngày theo nước + chữ gõ.
/// - Gợi ý địa chỉ: <see cref="IAddressSuggestionProvider"/> (hiện là Geoapify), cache 1 ngày theo nước + chữ gõ để tiết kiệm lượt gọi.
/// API ngoài lỗi / chậm thì trả rỗng, không làm hỏng form tạo đơn.
/// </summary>
internal sealed partial class GeoLookupService(
    HttpClient http, IPostalCodeProvider postalProvider, IAddressSuggestionProvider addressProvider, IMemoryCache cache,
    ILogger<GeoLookupService> logger) : IGeoLookup
{
    public const string CountriesUrl = "https://cdn.jsdelivr.net/npm/world-countries@5.1.0/countries.json";

    [GeneratedRegex("^[A-Z0-9][A-Z0-9 -]{1,11}$")]
    private static partial Regex PostalPattern();

    public async Task<IReadOnlyList<CountryInfo>> GetCountriesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue("geo:countries", out IReadOnlyList<CountryInfo>? cached) && cached is not null) return cached;
        try
        {
            await using var stream = await http.GetStreamAsync(CountriesUrl, cancellationToken);
            var countries = GeoParsers.ParseCountries(await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken));
            if (countries.Count > 0) cache.Set("geo:countries", countries, TimeSpan.FromDays(1));
            return countries;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Không lấy được danh sách quốc gia (world-countries)");
            return [];
        }
    }

    public async Task<PostalInfo?> LookupPostalAsync(string countryCode, string postalCode, CancellationToken cancellationToken)
    {
        if (GeoParsers.NormalizePostal(countryCode, postalCode) is not { } normalized || !PostalPattern().IsMatch(normalized.PostalCode)) return null;
        var (cc, postal) = normalized;

        var key = $"geo:postal:{cc}:{postal}";
        if (cache.TryGetValue(key, out PostalInfo? cached)) return cached;

        PostalInfo? info = null;
        foreach (var candidate in GeoParsers.PostalCandidates(cc, postal))
        {
            var result = await postalProvider.LookupAsync(cc, candidate, cancellationToken);
            if (result.Status == PostalLookupStatus.Unavailable) return null; // lỗi tạm thời → không cache, lần sau thử lại
            if (result.Info is { } found)
            {
                info = found with { PostalCode = postal };
                break;
            }
        }
        cache.Set(key, info, info is null ? TimeSpan.FromHours(6) : TimeSpan.FromDays(7));
        return info;
    }

    public async Task<IReadOnlyList<PostalSuggestion>> SearchPostalAsync(string countryCode, string prefix, CancellationToken cancellationToken)
    {
        if (GeoParsers.NormalizePostal(countryCode, prefix) is not { } normalized || !PostalPattern().IsMatch(normalized.PostalCode)) return [];
        var (cc, postal) = normalized;

        var key = $"geo:postal-search:{cc}:{postal}";
        if (cache.TryGetValue(key, out IReadOnlyList<PostalSuggestion>? cached) && cached is not null) return cached;

        var result = await postalProvider.SearchAsync(cc, postal, cancellationToken);
        if (!result.Available) return [];
        IReadOnlyList<PostalSuggestion> items = result.Items;
        if (items.Count == 0 && await LookupPostalAsync(cc, postal, cancellationToken) is { } info)
            items = info.Places.Select(p => new PostalSuggestion(info.PostalCode, p.City, p.State, p.StateCode, p.Area)).ToList();
        cache.Set(key, items, TimeSpan.FromDays(1));
        return items;
    }

    public async Task<IReadOnlyList<AddressSuggestion>> SuggestAddressesAsync(string countryCode, string query, CancellationToken cancellationToken)
    {
        if (GeoParsers.NormalizeAddressQuery(countryCode, query) is not { } normalized) return [];
        var (cc, text) = normalized;

        var key = $"geo:address:{cc}:{text.ToLowerInvariant()}";
        if (cache.TryGetValue(key, out IReadOnlyList<AddressSuggestion>? cached) && cached is not null) return cached;

        var result = await addressProvider.SuggestAsync(cc, text, cancellationToken);
        if (result.Available) cache.Set(key, result.Items, TimeSpan.FromDays(1));
        return result.Items;
    }
}
