using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>Danh sách "Đơn nháp &amp; chưa in" (500 đơn mới nhất trong phạm vi của tài khoản).</summary>
internal sealed record GetDraftsQuery : IRequest<IReadOnlyList<DraftDto>>;

internal sealed class GetDraftsHandler(ShipmentsDbContext db, OrderAccess access) : IRequestHandler<GetDraftsQuery, IReadOnlyList<DraftDto>>
{
    private const int ListLimit = 500;

    public async Task<IReadOnlyList<DraftDto>> Handle(GetDraftsQuery q, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(ct);
        var drafts = await access.Apply(db.Drafts().AsNoTracking(), scope)
            .OrderByDescending(o => o.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        var (packages, items) = await DraftStore.LinesAsync(db, drafts.Select(d => d.Id).ToList(), ct);
        return drafts.Select(d => DraftOrders.ToDto(d, packages[(int)d.Id].ToList(), items[(int)d.Id].ToList())).ToList();
    }
}
