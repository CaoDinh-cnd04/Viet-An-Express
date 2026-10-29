using System.ComponentModel.DataAnnotations;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo.GeoNames;

/// <summary>
/// Cấu hình GeoNames (section "GeoNames"). Tài khoản miễn phí đăng ký tại geonames.org, bật "Free Web Services".
/// Username chỉ nằm ở backend (User Secrets khi dev, appsettings.Production.json / biến môi trường GeoNames__Username khi chạy thật) —
/// frontend không bao giờ gọi thẳng GeoNames.
/// </summary>
internal sealed class GeoNamesOptions
{
    public const string Section = "GeoNames";

    /// <summary>Endpoint HTTPS.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = "https://secure.geonames.org";

    /// <summary>Tên tài khoản GeoNames. Để trống thì tắt tra mã bưu chính (khách tự nhập thành phố / bang).</summary>
    public string Username { get; set; } = "";

    /// <summary>Thời gian chờ mỗi lần gọi (giây).</summary>
    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 6;
}
