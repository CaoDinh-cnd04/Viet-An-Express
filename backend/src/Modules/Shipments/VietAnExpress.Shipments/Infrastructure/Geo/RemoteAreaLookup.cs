using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace VietAnExpress.Shipments.Infrastructure.Geo;

/// <summary>1 hãng tính phụ phí vùng sâu vùng xa (VSVX / ODA) ở địa chỉ người nhận. Tier = mức của FedEx (Tier A/B/C), null ở hãng khác.</summary>
internal sealed record RemoteAreaHit(string Carrier, string? Tier);

/// <summary>1 dòng bảng dbo.VungXauVungXa (bảng hệ thống cũ, hàm Load_FEDEX_Remote_Area tra bảng này).</summary>
internal sealed record RemoteAreaRow(string? Carrier, string? City, string? BeginPostal, string? EndPostal, string? ToParcel);

/// <summary>Tra VSVX theo nước + mã bưu chính + thành phố, như hệ thống cũ.</summary>
internal interface IRemoteAreaLookup
{
    /// <summary>Các hãng tính phụ phí ở địa chỉ này; rỗng khi không thuộc vùng nào hoặc thiếu dữ liệu.</summary>
    Task<IReadOnlyList<RemoteAreaHit>> FindAsync(string countryCode, string? postalCode, string? city, CancellationToken cancellationToken);
}

/// <summary>
/// Đọc dbo.VungXauVungXa (không thuộc migration — truy vấn SQL trực tiếp, không đưa vào model EF), mỗi nước cache 1 ngày
/// (dữ liệu tĩnh, chỉ đổi khi nhập lại file của hãng), khớp bằng <see cref="RemoteAreaMatcher"/>.
/// </summary>
internal sealed class RemoteAreaLookup(ShipmentsDbContext db, IMemoryCache cache, ILogger<RemoteAreaLookup> logger) : IRemoteAreaLookup
{
    public async Task<IReadOnlyList<RemoteAreaHit>> FindAsync(string countryCode, string? postalCode, string? city, CancellationToken cancellationToken)
    {
        var cc = countryCode.Trim().ToUpperInvariant();
        if (cc.Length != 2 || !cc.All(char.IsAsciiLetterUpper)) return [];
        if (string.IsNullOrWhiteSpace(postalCode) && string.IsNullOrWhiteSpace(city)) return [];

        // Mọi hãng trong bảng (FedEx, UPS, Au-Post, CT-VA…): 1 mã bưu chính có thể thuộc VSVX của nhiều hãng cùng lúc.
        var key = $"geo:remote:all:{cc}";
        if (!cache.TryGetValue(key, out IReadOnlyList<RemoteAreaRow>? rows) || rows is null)
        {
            try
            {
                rows = await db.Database
                    .SqlQuery<RemoteAreaRow>($"""
                        SELECT DichVu AS Carrier, City, [Begin Postal Code] AS BeginPostal, [End Postal Code] AS EndPostal, [To Parcel Services] AS ToParcel
                        FROM dbo.VungXauVungXa WHERE [Country Code] = {cc}
                        """)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
            }
            catch (Exception e) when (e is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                // Chưa có bảng (môi trường mới) / mất kết nối: form vẫn dùng được, chỉ không cảnh báo VSVX.
                logger.LogWarning(e, "Không đọc được dbo.VungXauVungXa cho {Country}", cc);
                return [];
            }
            cache.Set(key, rows, TimeSpan.FromDays(1));
        }
        return RemoteAreaMatcher.Match(rows, cc, postalCode, city);
    }
}

/// <summary>Khớp địa chỉ với các dòng VSVX — hàm thuần, có test.</summary>
internal static partial class RemoteAreaMatcher
{
    /// <summary>
    /// Dòng khớp khi: có khoảng mã → mã khách nằm trong khoảng; có thành phố → trùng thành phố (không phân biệt hoa thường, dấu).
    /// Dòng có cả hai (Au-Post) phải khớp cả hai. Mỗi hãng 1 kết quả (tier đầu tiên khác "No"), FedEx đứng đầu.
    /// </summary>
    public static IReadOnlyList<RemoteAreaHit> Match(IEnumerable<RemoteAreaRow> rows, string countryCode, string? postalCode, string? city)
    {
        var postal = (postalCode ?? "").Trim().ToUpperInvariant();
        var cityKey = CityKey(city);
        var hits = new List<RemoteAreaHit>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Carrier)) continue;
            var hasPostal = !string.IsNullOrWhiteSpace(row.BeginPostal);
            var hasCity = !string.IsNullOrWhiteSpace(row.City);
            if (!hasPostal && !hasCity) continue;
            if (hasPostal && !PostalInRange(countryCode, postal, row.BeginPostal!, row.EndPostal)) continue;
            if (hasCity && (cityKey.Length == 0 || CityKey(row.City) != cityKey)) continue;

            var carrier = row.Carrier.Trim();
            var tier = row.ToParcel?.Trim() is { Length: > 0 } t && !t.Equals("No", StringComparison.OrdinalIgnoreCase) ? t : null;
            var existing = hits.FindIndex(h => h.Carrier.Equals(carrier, StringComparison.OrdinalIgnoreCase));
            if (existing < 0) hits.Add(new RemoteAreaHit(carrier, tier));
            else if (hits[existing].Tier is null && tier is not null) hits[existing] = hits[existing] with { Tier = tier };
        }
        // FedEx trước (như hệ thống cũ), các hãng khác giữ thứ tự trong bảng.
        return hits.OrderBy(h => h.Carrier.Equals("Fedex", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
    }

    /// <summary>
    /// Mã khách nằm trong [begin, end]: so phần đầu mã khách (cùng độ dài với mốc) — dữ liệu hãng thường ghi theo phần đầu
    /// (Nhật "00100" = 5 số đầu của mã 7 số, Mỹ nhận cả ZIP+4). Bỏ khoảng trắng / gạch nối. Anh so theo vùng (xem <see cref="GbInRange"/>).
    /// </summary>
    public static bool PostalInRange(string countryCode, string postal, string begin, string? end)
    {
        if (postal.Length == 0) return false;
        if (countryCode == "GB") return GbInRange(postal, begin, end ?? begin);

        var p = Compact(postal);
        var b = Compact(begin);
        var e = Compact(string.IsNullOrWhiteSpace(end) ? begin : end);
        if (b.Length == 0 || p.Length < b.Length) return false;
        return string.CompareOrdinal(p[..b.Length], b) >= 0
            && string.CompareOrdinal(p[..Math.Min(e.Length, p.Length)], e) <= 0;
    }

    [GeneratedRegex(@"^([A-Z]{1,2})(\d{1,2})?([A-Z])?(?:\s+(\d))?$")]
    private static partial Regex GbBound();

    [GeneratedRegex(@"^([A-Z]{1,2})(\d{1,2})([A-Z])?\s*(\d)?")]
    private static partial Regex GbPostal();

    /// <summary>
    /// Anh: dữ liệu theo vùng — "IM" (cả vùng), "HS1X" / "HS1" (quận 1, X = mọi mã), "IV4"–"IV11" (quận 4 đến 11), "IV1 3" (quận 1 khu 3).
    /// Mã khách "HS1 2AB" / "HS12AB" → vùng HS, quận 1, khu 2.
    /// </summary>
    public static bool GbInRange(string postal, string begin, string end)
    {
        var p = postal.Trim().ToUpperInvariant();
        // Không có khoảng trắng: 3 ký tự cuối là phần sau ("SW1A1AA" → "SW1A 1AA").
        if (!p.Contains(' ') && p.Length >= 5) p = $"{p[..^3]} {p[^3..]}";
        var pm = GbPostal().Match(p);
        if (!pm.Success) return false;
        var area = pm.Groups[1].Value;
        var district = int.Parse(pm.Groups[2].Value, CultureInfo.InvariantCulture);
        int? sector = pm.Groups[4].Success ? int.Parse(pm.Groups[4].Value, CultureInfo.InvariantCulture) : null;

        var (bArea, bDistrict, bSector) = ParseGbBound(begin);
        var (eArea, eDistrict, eSector) = ParseGbBound(end); // end = begin khi chỉ có 1 mốc
        if (bArea is null || area != bArea || (eArea is not null && area != eArea)) return false;
        if (bDistrict is null) return true; // cả vùng
        var to = eDistrict ?? bDistrict;
        if (district < bDistrict || district > to) return false;
        if (district == bDistrict && bSector is not null && (sector is null || sector < bSector)) return false;
        if (district == to && eSector is { } maxSector && (sector is null || sector > maxSector)) return false;
        return true;
    }

    private static (string? Area, int? District, int? Sector) ParseGbBound(string bound)
    {
        var m = GbBound().Match(bound.Trim().ToUpperInvariant());
        if (!m.Success) return (null, null, null);
        int? district = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : null;
        int? sector = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : null;
        return (m.Groups[1].Value, district, sector);
    }

    private static string Compact(string s) => new(s.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    /// <summary>So thành phố: chữ hoa, bỏ dấu, gộp khoảng trắng ("São Paulo" = "SAO PAULO").</summary>
    public static string CityKey(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return "";
        var decomposed = city.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
