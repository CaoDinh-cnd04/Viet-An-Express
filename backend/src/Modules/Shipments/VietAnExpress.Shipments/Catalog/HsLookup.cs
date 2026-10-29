using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

/// <param name="Mode">"verified" = chỉ mặt hàng Việt An đã xác minh; "catalog" = toàn bộ danh mục của nước đó.</param>
internal sealed record HsLookupQuery(string? Country, string? Search, string? Mode) : IRequest<IReadOnlyList<HsLookupItemDto>>;

internal sealed class HsLookupHandler(ShipmentsDbContext db) : IRequestHandler<HsLookupQuery, IReadOnlyList<HsLookupItemDto>>
{
    public async Task<IReadOnlyList<HsLookupItemDto>> Handle(HsLookupQuery q, CancellationToken ct)
    {
        if (HsLookupRules.Parse(q.Country, q.Search, q.Mode) is not { } f) return [];

        var rows = await HsLookupRules.Build(db.Set<HsCodeEntry>().AsNoTracking(), f).ToListAsync(ct);
        return [.. rows.Select(HsLookupRules.Present).OfType<HsLookupItemDto>()];
    }
}
