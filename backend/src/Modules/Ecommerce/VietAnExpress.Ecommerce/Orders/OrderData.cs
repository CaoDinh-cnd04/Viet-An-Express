using System.Globalization;
using System.Text.Json;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>Dòng sản phẩm lưu trong dbo.DonTMDT.San_Pham — trùng <c>EcomProduct</c> của frontend.</summary>
internal sealed record EcomProductDto(string Name, string Sku, int Qty, decimal FobPrice, decimal SellingPrice, string? HsCode = null);

/// <summary>Chuẩn hóa trường dữ liệu đơn từ sàn / file và tìm chỗ cần bổ sung trước khi tạo bill. Hàm thuần — có test.</summary>
internal static class OrderData
{
    /// <summary>JSON của cột San_Pham / khai báo hải quan (camelCase như frontend).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Mã gọi quốc tế của các nước hay gửi — dùng thêm vào số nội địa (vd 080… ở Nhật → +8180…).</summary>
    private static readonly Dictionary<string, string> CallingCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = "1", ["CA"] = "1", ["JP"] = "81", ["KR"] = "82", ["CN"] = "86", ["TW"] = "886", ["HK"] = "852", ["MO"] = "853",
        ["SG"] = "65", ["MY"] = "60", ["TH"] = "66", ["ID"] = "62", ["PH"] = "63", ["VN"] = "84", ["KH"] = "855", ["IN"] = "91",
        ["AU"] = "61", ["NZ"] = "64", ["GB"] = "44", ["IE"] = "353", ["DE"] = "49", ["FR"] = "33", ["IT"] = "39", ["ES"] = "34",
        ["PT"] = "351", ["NL"] = "31", ["BE"] = "32", ["LU"] = "352", ["CH"] = "41", ["AT"] = "43", ["DK"] = "45", ["SE"] = "46",
        ["NO"] = "47", ["FI"] = "358", ["PL"] = "48", ["CZ"] = "420", ["HU"] = "36", ["GR"] = "30", ["RO"] = "40", ["AE"] = "971",
        ["SA"] = "966", ["QA"] = "974", ["IL"] = "972", ["TR"] = "90", ["MX"] = "52", ["BR"] = "55", ["CL"] = "56", ["ZA"] = "27"
    };

    /// <summary>
    /// Số điện thoại người nhận dạng quốc tế: bỏ khoảng trắng / gạch / ngoặc / dấu ' của Excel;
    /// số nội địa (bắt đầu bằng 0, hoặc thiếu mã nước) được thêm mã gọi của nước đến khi biết.
    /// </summary>
    public static string? NormalizePhone(string? phone, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        if (phone.TrimStart('\'', ' ').StartsWith('+')) return "+" + digits;
        if (digits.StartsWith("00", StringComparison.Ordinal)) return "+" + digits[2..];
        if (countryCode is null || !CallingCodes.TryGetValue(countryCode, out var cc)) return digits;
        if (digits.StartsWith('0')) return $"+{cc}{digits.TrimStart('0')}";
        // Mỹ / Canada: 10 số không kèm mã nước.
        if (cc == "1" && digits.Length == 10) return "+1" + digits;
        return digits.StartsWith(cc, StringComparison.Ordinal) && digits.Length > cc.Length + 6 ? "+" + digits : digits;
    }

    /// <summary>Mã bưu chính: bỏ dấu ' Excel thêm để giữ số 0 đầu, viết hoa, gọn khoảng trắng.</summary>
    public static string? NormalizePostal(string? postal) =>
        postal?.Trim().TrimStart('\'').Trim().ToUpperInvariant() is { Length: > 0 } p ? string.Join(' ', p.Split(' ', StringSplitOptions.RemoveEmptyEntries)) : null;

    /// <summary>"JP" → "Japan" (tên tiếng Anh như trên nhãn). Không tra được → trả lại mã.</summary>
    public static string? CountryName(string? code)
    {
        if (code is not { Length: 2 }) return null;
        try { return new RegionInfo(code).EnglishName; }
        catch (ArgumentException) { return code.ToUpperInvariant(); }
    }

    /// <summary>Chỉ chữ Latin (kể cả có dấu như é, ñ, ø), số và ký hiệu thường gặp — nhãn hãng bay không in được chữ Nhật / Hàn / Trung…</summary>
    public static bool IsLatin(string? text) =>
        string.IsNullOrEmpty(text) || text.All(c => c < 0x0250 || char.IsWhiteSpace(c) || c is '‘' or '’' or '“' or '”' or '–' or '—' or '№');

    /// <summary>
    /// Trường bắt buộc còn thiếu trước khi xác nhận gửi — chỉ những gì sàn nào cũng có (người nhận, địa chỉ, SĐT, sản phẩm).
    /// Cân nặng, mã HS, chữ Latin không bắt buộc: Việt An cân / khai hải quan khi nhận hàng (người dùng đã chọn).
    /// </summary>
    /// <param name="products">Sản phẩm đã đọc sẵn (nếu có) — tránh deserialize ProductsJson lần nữa.</param>
    public static IReadOnlyList<string> Issues(MarketplaceOrder o, IReadOnlyList<EcomProductDto>? products = null)
    {
        if (o.Bill is not null) return [];
        var r = o.Recipient;
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name)) issues.Add("Thiếu tên người nhận");
        if (string.IsNullOrWhiteSpace(r.Address1) || string.IsNullOrWhiteSpace(r.CountryCode)) issues.Add("Thiếu địa chỉ / nước đến");
        if (string.IsNullOrWhiteSpace(r.Phone)) issues.Add("Thiếu số điện thoại người nhận");
        if ((products ?? Products(o)).Count == 0) issues.Add("Chưa có sản phẩm");
        return issues;
    }

    public static List<EcomProductDto> Products(MarketplaceOrder o) =>
        o.ProductsJson is { } json ? JsonSerializer.Deserialize<List<EcomProductDto>>(json, Json) ?? [] : [];
}
