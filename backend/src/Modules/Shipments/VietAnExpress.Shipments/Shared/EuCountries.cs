using System.Collections.Frozen;
using System.Text.Json;

namespace VietAnExpress.Shipments.Shared;

internal static class EuCountries
{
    private static readonly Dictionary<string, string> Countries = Load();
    public static readonly FrozenSet<string> EU_COUNTRIES = Countries.Keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsEuCountry(string? code) => EU_COUNTRIES.Contains(code?.Trim() ?? "");

    // Ưu tiên nước đến thực tế, tránh mã ISO cũ còn sót lại khi client đổi tên nước.
    public static string? ReceiverCode(string? country, string? countryCode) =>
        !string.IsNullOrWhiteSpace(country) ? CodeOf(country) : CodeOf(countryCode);

    public static string? CodeOf(string? country)
    {
        var text = string.Join(' ', (country ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (IsEuCountry(text)) return text.ToUpperInvariant();
        if (text.Equals("Czech Republic", StringComparison.OrdinalIgnoreCase)) return "CZ";
        return Countries.FirstOrDefault(c => c.Value.Equals(text, StringComparison.OrdinalIgnoreCase)).Key;
    }

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(EuCountries).Assembly.GetManifestResourceStream("VietAnExpress.Shipments.eu-countries.json")
            ?? throw new InvalidOperationException("Thiếu dữ liệu quốc gia EU");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
