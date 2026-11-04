using VietAnExpress.Shipments.Printing;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>
/// Nước đến bắt buộc khai Tỉnh / Bang (Cnee_State): Mỹ, Canada, Úc — hãng cần để giao hàng. Dùng chung cho nhập Excel và In bill ở Đơn nháp;
/// form tạo đơn ở web có cùng danh sách (shared/config/domain.ts STATE_REQUIRED_COUNTRIES). Hàm thuần — có test.
/// </summary>
internal static class ReceiverStateRule
{
    public static readonly IReadOnlySet<string> Countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "US", "CA", "AU" };

    /// <summary>Mã nước (ISO-2) form gửi; thiếu thì đổi từ tên nước tiếng Anh.</summary>
    public static bool Required(string? countryCode, string? countryName)
    {
        var code = countryCode?.Trim() is { Length: 2 } cc ? cc : BillA4.CountryCode(countryName);
        return Countries.Contains(code);
    }
}
