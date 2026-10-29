
namespace VietAnExpress.Shipments.Geo;

/// <summary>
/// 1 dòng gợi ý địa chỉ người nhận. <c>Address1</c> rỗng khi gợi ý chỉ tới cấp thành phố / mã bưu chính.
/// </summary>
internal sealed record AddressSuggestion(
    string Label, string Address1, string? City, string? State, string? StateCode, string? PostalCode, string CountryCode);

internal sealed record AddressSuggestionResult(bool Available, IReadOnlyList<AddressSuggestion> Items)
{
    /// <summary>Nguồn lỗi / hết lượt / chưa cấu hình — không cache, lần sau thử lại.</summary>
    public static readonly AddressSuggestionResult Unavailable = new(false, []);
    public static AddressSuggestionResult Found(IReadOnlyList<AddressSuggestion> items) => new(true, items);
}

/// <summary>Nguồn gợi ý địa chỉ (adapter tới API ngoài, hiện là Geoapify). Nhận mã nước ISO 2 chữ hoa và chuỗi khách gõ đã kiểm tra.</summary>
internal interface IAddressSuggestionProvider
{
    Task<AddressSuggestionResult> SuggestAsync(string countryCode, string query, CancellationToken cancellationToken);
}
