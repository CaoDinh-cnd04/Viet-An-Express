using Microsoft.EntityFrameworkCore;

namespace VietAnExpress.Shipments.Infrastructure.Legacy;

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
}
