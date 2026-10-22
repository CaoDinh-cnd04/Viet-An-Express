namespace VietAnExpress.Shipments.Application.Orders.Documents;

/// <summary>
/// Thông tin công ty in trên bill, invoice, công văn (section "Company" trong appsettings).
/// Mặc định lấy theo trang tracking hiện tại của Việt An.
/// </summary>
internal sealed class CompanyInfo
{
    public const string Section = "Company";

    public string Name { get; init; } = "Công ty TNHH Dịch vụ Giao nhận Quốc tế Việt An";
    public string EnglishName { get; init; } = "Viet An Express International Co., Ltd";
    public string Address { get; init; } = "14 Sâm Sơn, Phường 4, Quận Tân Bình, TP. Hồ Chí Minh";
    /// <summary>Theo mẫu bill hệ thống cũ: Tel là tổng đài, Hotline là di động.</summary>
    public string Phone { get; init; } = "+8428 3948 3949";
    public string Hotline { get; init; } = "+84 909 805 845";
    public string Website { get; init; } = "www.Vietanexpress.com.vn";
    /// <summary>Địa chỉ portal — mã QR trên bill trỏ tới {PortalUrl}/tracking/{số bill}.</summary>
    public string PortalUrl { get; init; } = "http://beta.vietanexpress.com.vn";
    public string TaxCode { get; init; } = "0310278855";
}
