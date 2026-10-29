using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo;

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
