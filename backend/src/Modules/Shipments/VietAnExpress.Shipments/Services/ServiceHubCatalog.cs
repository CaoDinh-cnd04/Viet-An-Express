using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Services;

/// <summary>Danh mục hãng / dịch vụ → hub (tuyến) của hệ thống cũ.</summary>
internal interface IServiceHubCatalog
{
    /// <summary>Hãng theo thứ tự danh mục, mỗi hãng kèm hub; rỗng khi chưa có bảng / không đọc được.</summary>
    Task<IReadOnlyList<ServiceCarrierDto>> GetAsync(CancellationToken cancellationToken);
}

/// <summary>1 dòng đọc từ 2 bảng danh mục.</summary>
internal sealed record ServiceHubRow(int CarrierId, string? Carrier, int? CarrierSort, string? Hub, string? HubSort, string? Code);

/// <summary>
/// Đọc danh mục dịch vụ của hệ thống cũ (không thuộc migration — truy vấn SQL trực tiếp, không đưa vào model EF):
/// <c>dbo.TGroupBaseCode</c> dòng <c>ParentID = 21</c> = hãng / dịch vụ (DHL, Fedex, Chuyên tuyến…),
/// <c>dbo.TBaseCode</c> cùng <c>GroupID</c> = hub / tuyến (<c>Name</c>), <c>Option2</c> = mã ngắn in trên bill cũ.
/// MaVanDon.Dich_Vu = "Hãng|Hub" phải trùng đúng chữ 2 bảng này — proc in bill cũ (p_MVD_Print_Bill) và CMS khớp theo chuỗi.
/// Trước 06/11/2026 frontend chép tay danh sách (shared/config/domain.ts) — dễ lệch khi CS thêm / sửa tuyến ở CMS.
/// Cache 10 phút.
/// </summary>
internal sealed class ServiceHubCatalog(ShipmentsDbContext db, IMemoryCache cache, ILogger<ServiceHubCatalog> logger) : IServiceHubCatalog
{
    /// <summary>TGroupBaseCode.ParentID của nhóm "dịch vụ vận chuyển" — như proc p_MVD_Print_Bill.</summary>
    public const int CarrierParentId = 21;

    private const string CacheKey = "services:hubs";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<ServiceCarrierDto>> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<ServiceCarrierDto>? cached) && cached is not null) return cached;
        IReadOnlyList<ServiceHubRow> rows;
        try
        {
            rows = await db.Database
                .SqlQuery<ServiceHubRow>($"""
                    SELECT GB.GroupID AS CarrierId, GB.Name AS Carrier, GB.SortOrder AS CarrierSort,
                           B.Name AS Hub, B.SortOrder AS HubSort, CAST(B.Option2 AS nvarchar(250)) AS Code
                    FROM dbo.TGroupBaseCode GB
                    LEFT JOIN dbo.TBaseCode B ON B.GroupID = GB.GroupID
                    WHERE GB.ParentID = {CarrierParentId}
                    """)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
        catch (Exception e) when (e is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            // Chưa có 2 bảng danh mục (vd laptop dev) / mất kết nối: trả rỗng, frontend dùng danh sách dự phòng. Không cache để lần sau thử lại.
            logger.LogWarning(e, "Không đọc được danh mục dịch vụ dbo.TGroupBaseCode / dbo.TBaseCode");
            return [];
        }
        var result = ServiceHubRules.Build(rows);
        cache.Set(CacheKey, result, CacheFor);
        return result;
    }
}

/// <summary>Gom dòng danh mục thành hãng → hub. Hàm thuần — có test.</summary>
internal static class ServiceHubRules
{
    /// <summary>
    /// Bỏ hãng / hub trống tên và trùng (không phân biệt hoa thường); giữ nguyên chữ như danh mục (Dich_Vu khớp đúng chữ).
    /// Thứ tự: SortOrder (hub: cột chữ, số thì so theo số; trống xếp cuối) rồi tên. Hãng không có hub vẫn trả (danh mục hub rỗng).
    /// </summary>
    public static IReadOnlyList<ServiceCarrierDto> Build(IEnumerable<ServiceHubRow> rows) =>
        [.. rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Carrier))
            .GroupBy(r => r.Carrier!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Carrier: g.Key, Sort: g.Min(r => r.CarrierSort) ?? int.MaxValue, Id: g.Min(r => r.CarrierId),
                Hubs: (IReadOnlyList<ServiceHubDto>)[.. g
                    .Where(r => !string.IsNullOrWhiteSpace(r.Hub))
                    .GroupBy(r => r.Hub!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(h => h.First())
                    .OrderBy(r => SortKey(r.HubSort)).ThenBy(r => r.HubSort?.Trim(), StringComparer.Ordinal).ThenBy(r => r.Hub!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(r => new ServiceHubDto(r.Hub!.Trim(), string.IsNullOrWhiteSpace(r.Code) ? null : r.Code.Trim()))]))
            .OrderBy(c => c.Sort).ThenBy(c => c.Id)
            .Select(c => new ServiceCarrierDto(c.Carrier, c.Hubs))];

    /// <summary>
    /// Mã dịch vụ in dưới số bill (bill A4, nhãn A6) đúng như proc in bill cũ p_MVD_Print_Bill: Dich_Vu "Hãng|Hub" khớp danh mục
    /// (không phân biệt hoa thường) → TBaseCode.Option2 (vd "Chuyên tuyến|AU_Toll (T.Phẩm/M.Phẩm)" → "CT-Au_Toll (Tp/Mp)",
    /// "DHL|Singapore" → "DHL-SIN"); không có trong danh mục / không có mã → "Hãng - Hub" (REPLACE '|' bằng ' - ' như proc cũ).
    /// Hàm thuần — có test.
    /// </summary>
    public static string PrintCode(IReadOnlyList<ServiceCarrierDto> catalog, string? serviceName)
    {
        var text = serviceName?.Trim() ?? "";
        var parts = text.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            var code = catalog.FirstOrDefault(c => string.Equals(c.Carrier, parts[0], StringComparison.OrdinalIgnoreCase))?
                .Hubs.FirstOrDefault(h => string.Equals(h.Name, parts[1], StringComparison.OrdinalIgnoreCase))?.Code;
            if (!string.IsNullOrWhiteSpace(code)) return code;
        }
        return text.Replace("|", " - ");
    }

    /// <summary>TBaseCode.SortOrder là nchar(10): "2" &lt; "10" theo số; chữ / trống xếp sau.</summary>
    private static long SortKey(string? sort) => long.TryParse(sort?.Trim(), out var n) ? n : long.MaxValue;
}

/// <summary>1 hãng / dịch vụ và các hub (tuyến) của nó.</summary>
internal sealed record ServiceCarrierDto(string Carrier, IReadOnlyList<ServiceHubDto> Hubs);

/// <summary>1 hub: tên như danh mục (ghi MaVanDon.Dich_Vu = "Hãng|Tên"), mã ngắn in trên bill cũ (TBaseCode.Option2).</summary>
internal sealed record ServiceHubDto(string Name, string? Code);
