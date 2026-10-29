
namespace VietAnExpress.Shipments.Geo;

/// <summary>Kết quả 1 lần hỏi nguồn mã bưu chính.</summary>
internal enum PostalLookupStatus
{
    /// <summary>Có dữ liệu.</summary>
    Found,
    /// <summary>Nguồn trả lời bình thường nhưng không có mã này — được cache.</summary>
    NotFound,
    /// <summary>Nguồn lỗi / hết lượt / chưa cấu hình — không cache, lần sau thử lại.</summary>
    Unavailable
}

internal sealed record PostalLookupResult(PostalLookupStatus Status, PostalInfo? Info = null)
{
    public static readonly PostalLookupResult NotFound = new(PostalLookupStatus.NotFound);
    public static readonly PostalLookupResult Unavailable = new(PostalLookupStatus.Unavailable);
    public static PostalLookupResult Found(PostalInfo info) => new(PostalLookupStatus.Found, info);
}

/// <summary>
/// Nguồn tra mã bưu chính (adapter tới API bên ngoài). Hiện dùng GeoNames; đổi / thêm nguồn chỉ cần cài interface này
/// và đăng ký trong ShipmentsModule — API, cache, kiểm tra đầu vào ở <see cref="GeoLookupService"/> giữ nguyên.
/// Nhận mã đã chuẩn hóa (mã nước ISO 2 chữ hoa, mã bưu chính chữ hoa, 1 dấu cách).
/// </summary>
internal interface IPostalCodeProvider
{
    Task<PostalLookupResult> LookupAsync(string countryCode, string postalCode, CancellationToken cancellationToken);

    /// <summary>Các mã bắt đầu bằng <paramref name="prefix"/> (gợi ý khi gõ).</summary>
    Task<PostalSearchResult> SearchAsync(string countryCode, string prefix, CancellationToken cancellationToken);
}

/// <summary>Kết quả gợi ý mã bưu chính. Available = false khi nguồn lỗi / chưa cấu hình (không cache).</summary>
internal sealed record PostalSearchResult(bool Available, IReadOnlyList<PostalSuggestion> Items)
{
    public static readonly PostalSearchResult Unavailable = new(false, []);
}
