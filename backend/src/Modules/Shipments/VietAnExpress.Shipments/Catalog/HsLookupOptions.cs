using System.ComponentModel.DataAnnotations;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>Cấu hình tra mã HS (mục "HsLookup" của appsettings).</summary>
internal sealed class HsLookupOptions
{
    public const string Section = "HsLookup";
    public const string WebsiteSource = "website";
    public const string DatabaseSource = "database";

    /// <summary>
    /// "website" (mặc định): dùng chung dữ liệu với trang https://vietanexpress.com/tra-hs-code (API plugin vae-hscode)
    /// — máy chủ chưa có bảng <c>dbo.Catalog_HsCode</c>. "database": đọc <c>dbo.Catalog_HsCode</c> như trước.
    /// </summary>
    [Required, RegularExpression("(?i)^(website|database)$")]
    public string Source { get; set; } = WebsiteSource;

    /// <summary>Gốc API tra mã HS của website: nối thêm "products" (đã xác minh) / "tariff" (biểu thuế) + ?country=&amp;q=.</summary>
    [Required, Url]
    public string WebsiteApiUrl { get; set; } = "https://vietanexpress.com/wp-json/vae-hscode/v1/";

    /// <summary>
    /// true (mặc định): "Tra danh mục mã" của Mỹ / Anh đọc thẳng biểu thuế chính thức (<see cref="UsTariffApiUrl"/>, <see cref="UkTariffApiUrl"/>);
    /// API đó lỗi thì tự chuyển sang nguồn <see cref="Source"/>. Úc và "Mặt hàng đã xác minh" luôn theo <see cref="Source"/>.
    /// </summary>
    public bool OfficialTariff { get; set; } = true;

    /// <summary>API công khai của https://hts.usitc.gov (HTSUS): search?keyword= và exportList?from=&amp;to=&amp;format=JSON.</summary>
    [Required, Url]
    public string UsTariffApiUrl { get; set; } = "https://hts.usitc.gov/reststop/";

    /// <summary>API UK Trade Tariff: search?q= và headings/{4 số}.</summary>
    [Required, Url]
    public string UkTariffApiUrl { get; set; } = "https://www.trade-tariff.service.gov.uk/api/v2/";

    [Range(2, 30)]
    public int TimeoutSeconds { get; set; } = 8;

    [Range(0, 1440)]
    public int CacheMinutes { get; set; } = 10;

    public bool UseWebsite => string.Equals(Source.Trim(), WebsiteSource, StringComparison.OrdinalIgnoreCase);
}
