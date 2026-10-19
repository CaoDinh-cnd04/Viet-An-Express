using System.Globalization;
using System.Text.RegularExpressions;

namespace VietAnExpress.Shipments.Application.Orders;

/// <summary>
/// Mã VA bill hiển thị: tiền tố chi nhánh + số 7 chữ số (OrderNumber, MAX+1 như hệ thống cũ) + mã nước đến ISO 2 chữ,
/// vd TP.HCM + 6003585 + US → "VAHCM6003585US". Lưu ở dbo.MaVanDon.VA_Bill lúc cấp bill; đơn cũ (VA_Bill NULL) hiện 7 số. Hàm thuần — có test.
/// </summary>
internal static partial class VaBillCode
{
    /// <summary>Tiền tố theo chi nhánh gửi hàng (khớp BRANCHES của frontend). Đổi / thêm chi nhánh: sửa ở đây.</summary>
    public static readonly IReadOnlyDictionary<string, string> BranchPrefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["TP.HCM"] = "VAHCM",
        ["Hà Nội"] = "VAHN",
        ["Huế"] = "VAHUE",
        ["Bảo Lộc"] = "VABL",
        ["Cần Thơ"] = "VACT"
    };

    /// <summary>Độ dài tối đa (cột VA_Bill nvarchar(30)).</summary>
    public const int MaxLength = 30;

    [GeneratedRegex(@"^VA[A-Z]{1,6}(\d{7})[A-Z]{2}$")]
    private static partial Regex CodePattern();

    /// <summary>Mã đầy đủ; null khi chi nhánh chưa có tiền tố hoặc thiếu mã nước 2 chữ (đơn đó hiện 7 số như cũ).</summary>
    public static string? Format(string? branch, long orderNumber, string? countryCode)
    {
        var cc = countryCode?.Trim().ToUpperInvariant() ?? "";
        if (branch is null || !BranchPrefixes.TryGetValue(branch.Trim(), out var prefix)) return null;
        if (cc.Length != 2 || !cc.All(char.IsAsciiLetterUpper)) return null;
        return $"{prefix}{orderNumber.ToString("D7", CultureInfo.InvariantCulture)}{cc}";
    }

    /// <summary>Chi nhánh từ mã đã lưu: "VAHCM6003585US" → "TP.HCM"; null khi không phải mã VA.</summary>
    public static string? BranchOf(string? code)
    {
        var m = CodePattern().Match(code?.Trim().ToUpperInvariant() ?? "");
        if (!m.Success) return null;
        var prefix = m.Value[..^9]; // bỏ 7 số + 2 chữ nước
        return BranchPrefixes.FirstOrDefault(p => p.Value == prefix).Key;
    }

    /// <summary>
    /// Đơn chưa in chưa có số: cột VA_Bill tạm giữ tiền tố chi nhánh (vd "VAHN") để khi in cấp đúng mã; chi nhánh chưa có tiền tố → null.
    /// </summary>
    public static string? DraftPrefix(string? branch) =>
        branch is not null && BranchPrefixes.TryGetValue(branch.Trim(), out var prefix) ? prefix : null;

    /// <summary>Chi nhánh từ tiền tố đã lưu ở đơn chưa in: "VAHN" → "Hà Nội"; khác → null.</summary>
    public static string? BranchOfPrefix(string? prefix)
    {
        var p = prefix?.Trim().ToUpperInvariant() ?? "";
        return BranchPrefixes.FirstOrDefault(x => x.Value == p).Key;
    }

    /// <summary>Mã nước người nhận từ mã đã lưu: "VAHCM6003585US" → "US"; null khi không phải mã VA.</summary>
    public static string? CountryOf(string? code)
    {
        var text = code?.Trim().ToUpperInvariant() ?? "";
        return CodePattern().IsMatch(text) ? text[^2..] : null;
    }

    /// <summary>Số VA từ chữ khách nhập: "6003585" hoặc "VAHCM6003585US" (không phân biệt hoa thường) → 6003585; dạng khác → null.</summary>
    public static long? Number(string? input)
    {
        var text = input?.Trim().ToUpperInvariant() ?? "";
        if (text.Length == 0) return null;
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return n;
        var m = CodePattern().Match(text);
        return m.Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }
}
