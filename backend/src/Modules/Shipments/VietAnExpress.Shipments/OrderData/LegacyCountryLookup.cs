using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

internal sealed record LegacyCountryRow(int Id, string? Code, string Name);

/// <summary>Đọc danh mục dbo.Countries để ghi ConsigneeCountryID; bảng cũ không thuộc model / migration của portal.</summary>
internal static class LegacyCountryLookup
{
    public static async Task<IReadOnlyList<LegacyCountryRow>> LoadAsync(ShipmentsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<LegacyCountryRow>($"""
            SELECT [CountryID] AS [Id], [CountryCode] AS [Code], [CountryName] AS [Name]
            FROM [dbo].[Countries]
            """).ToListAsync(ct);

    /// <summary>Ưu tiên mã nước; nháp cũ thiếu mã thì khớp tên. Không đoán ID khi thiếu hoặc trùng dữ liệu.</summary>
    public static int? FindId(IReadOnlyList<LegacyCountryRow> countries, string? code, string? name)
    {
        var countryCode = code?.Trim();
        var countryName = name?.Trim();
        if (string.IsNullOrEmpty(countryCode) && string.IsNullOrEmpty(countryName)) return null;

        var matches = countries.Where(c => !string.IsNullOrEmpty(countryCode)
            ? string.Equals(c.Code?.Trim(), countryCode, StringComparison.OrdinalIgnoreCase)
            : string.Equals(c.Name.Trim(), countryName, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    /// <summary>Mã 2 ký tự của nước đến để in: theo CountryID, không có thì khớp tên (vd "Korea, South" → KR). Hàm thuần — có test.</summary>
    public static string? CodeOf(IReadOnlyList<LegacyCountryRow> countries, int? id, string? name)
    {
        static string? Iso(LegacyCountryRow? c) => c?.Code?.Trim() is { Length: 2 } code ? code.ToUpperInvariant() : null;
        if (id is { } countryId && Iso(countries.FirstOrDefault(c => c.Id == countryId)) is { } byId) return byId;
        var countryName = name?.Trim();
        if (string.IsNullOrEmpty(countryName)) return null;
        var matches = countries.Where(c => string.Equals(c.Name.Trim(), countryName, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? Iso(matches[0]) : null;
    }
}
