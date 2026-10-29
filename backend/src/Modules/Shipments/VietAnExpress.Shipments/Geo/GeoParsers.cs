using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo;

/// <summary>Hàm thuần dùng chung (đọc dữ liệu nước, chuẩn hóa mã bưu chính) — có test.</summary>
internal static class GeoParsers
{
    /// <summary>
    /// world-countries / REST Countries: idd = { root: "+1", suffixes: ["201", …] }.
    /// 1 suffix → mã đầy đủ (Việt Nam "+84"); nhiều suffix (Mỹ, Canada: mã vùng) → chỉ lấy root "+1".
    /// </summary>
    public static IReadOnlyList<CountryInfo> ParseCountries(JsonDocument doc)
    {
        var list = new List<CountryInfo>();
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var code = c.TryGetProperty("cca2", out var cca2) ? cca2.GetString() : null;
            var name = c.TryGetProperty("name", out var n) && n.TryGetProperty("common", out var common) ? common.GetString() : null;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) continue;

            string? dial = null;
            if (c.TryGetProperty("idd", out var idd) && idd.TryGetProperty("root", out var root) && root.GetString() is { Length: > 0 } r)
            {
                var suffixes = idd.TryGetProperty("suffixes", out var s) && s.ValueKind == JsonValueKind.Array
                    ? s.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : [];
                dial = suffixes.Count == 1 ? r + suffixes[0] : r;
            }
            list.Add(new CountryInfo(code.ToUpperInvariant(), name, dial));
        }
        return list.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Mã nước ISO 2 chữ hoa; mã bưu chính chữ hoa, gộp khoảng trắng. Null nếu mã nước sai.</summary>
    public static (string CountryCode, string PostalCode)? NormalizePostal(string countryCode, string postalCode)
    {
        var cc = countryCode.Trim().ToUpperInvariant();
        if (cc.Length != 2 || !cc.All(char.IsAsciiLetterUpper)) return null;
        var postal = string.Join(' ', postalCode.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return (cc, postal);
    }

    /// <summary>Chữ khách gõ để gợi ý địa chỉ: gộp khoảng trắng, 3–120 ký tự. Null nếu mã nước sai hoặc chữ quá ngắn / dài.</summary>
    public static (string CountryCode, string Query)? NormalizeAddressQuery(string countryCode, string query)
    {
        var cc = countryCode.Trim().ToUpperInvariant();
        if (cc.Length != 2 || !cc.All(char.IsAsciiLetterUpper)) return null;
        var text = string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length is >= 3 and <= 120 ? (cc, text) : null;
    }

    /// <summary>
    /// Các dạng mã thử lần lượt: mã đầy đủ trước, rồi phần đầu của mã (một số nước chỉ có dữ liệu theo vùng):
    /// "SW1A 1AA" → "SW1A"; Anh gõ liền "SW1A1AA" → "SW1A"; Canada "H0H0H0" → "H0H".
    /// </summary>
    public static IReadOnlyList<string> PostalCandidates(string countryCode, string postal)
    {
        var list = new List<string> { postal };
        var compact = postal.Replace(" ", "");
        var prefix = postal.Contains(' ') ? postal.Split(' ')[0]
            : countryCode == "GB" && compact.Length is >= 5 and <= 7 ? compact[..^3]
            : countryCode == "CA" && compact.Length == 6 ? compact[..3]
            : null;
        if (prefix is { Length: >= 2 } && prefix != postal) list.Add(prefix);
        return list;
    }
                                                                                                                                                                                                                                                                                                                                                                                            }
