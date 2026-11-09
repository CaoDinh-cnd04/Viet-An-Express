using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

/// <param name="Mode">"verified" = chỉ mặt hàng Việt An đã xác minh; "catalog" = toàn bộ danh mục của nước đó.</param>
internal sealed record HsLookupQuery(string? Country, string? Search, string? Mode) : IRequest<IReadOnlyList<HsLookupItemDto>>;

/// <summary>
/// Nguồn theo <see cref="HsLookupOptions.Source"/>: API trang tra-hs-code của website (mặc định) hoặc bảng dbo.Catalog_HsCode.
/// "Tra danh mục mã" của Mỹ / Anh: biểu thuế chính thức (hts.usitc.gov / UK Trade Tariff); API đó lỗi thì dùng nguồn trên.
/// </summary>
internal sealed class HsLookupHandler(
    ShipmentsDbContext db, IHsWebsiteLookup website, IHsOfficialLookup official, IOptions<HsLookupOptions> options, ILogger<HsLookupHandler> logger)
    : IRequestHandler<HsLookupQuery, IReadOnlyList<HsLookupItemDto>>
{
    public async Task<IReadOnlyList<HsLookupItemDto>> Handle(HsLookupQuery q, CancellationToken ct)
    {
        if (HsLookupRules.Parse(q.Country, q.Search, q.Mode) is not { } f) return [];
        var country = q.Country!.Trim();
        var search = q.Search!.Trim();

        IReadOnlyList<HsLookupItemDto>? rows = null;
        if (!f.VerifiedOnly && official.Supports(country, f))
        {
            try
            {
                rows = await official.SearchAsync(country, f, search, ct);
            }
            catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || (e is TaskCanceledException && !ct.IsCancellationRequested))
            {
                logger.LogWarning(e, "Tra mã HS biểu thuế chính thức {Country} lỗi, chuyển sang nguồn {Source}", country, options.Value.Source);
            }
        }
        rows ??= options.Value.UseWebsite
            ? await website.SearchAsync(country, search, f.VerifiedOnly, ct)
            : await HsLookupRules.Build(db.Set<HsCodeEntry>().AsNoTracking(), f).ToListAsync(ct);
        return [.. rows.Select(HsLookupRules.Present).OfType<HsLookupItemDto>()];
    }
}
