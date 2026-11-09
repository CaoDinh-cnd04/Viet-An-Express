using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Orders;

/// <summary>
/// Tham số lọc giống hợp đồng frontend (OrderFilters). Type: DOC | PACK — nhận diện theo tên hàng (<see cref="LegacyDocumentRule"/>).
/// Không lọc chi nhánh: dbo.MaVanDon không có cột chi nhánh.
/// <c>Managed</c> = đơn mọi khách nhân viên Việt An phụ trách (/vietan/orders), dòng kèm mã / tên khách.
/// </summary>
internal sealed record GetOrdersQuery(
    string? Q,
    string? SearchField,
    string? Status,
    string? Type,
    DateOnly? FromDate,
    DateOnly? ToDate,
    decimal? WeightFrom,
    decimal? WeightTo,
    int? Page,
    int? PageSize,
    string? SortBy,
    string? SortDir,
    bool Managed = false) : IRequest<OrderListResponse>;

internal sealed class GetOrdersHandler(ShipmentsDbContext db, OrderAccess access, TimeProvider clock)
    : IRequestHandler<GetOrdersQuery, OrderListResponse>
{
    public async Task<OrderListResponse> Handle(GetOrdersQuery q, CancellationToken ct)
    {
        var today = VietnamTime.ToVietnam(clock.GetUtcNow()).Date;
        var scope = await access.ScopeAsync(q.Managed, ct);
        var query = access.Apply(db.LegacyOrders.AsNoTracking(), scope);
        query = OrderListFilter.Apply(query, q, await access.CustomerMatchesAsync(scope, q, ct));

        var statuses = OrderListFilter.Statuses(q.Status);
        var (page, size) = Paging.Normalize(q.Page, q.PageSize);
        var offset = (long)(page - 1) * size;

        // Số đếm theo trạng thái tính trên kết quả lọc TRỪ lọc trạng thái — để số trên các tab luôn đúng;
        // tổng đơn / kiện / cân thì theo đúng trạng thái đang lọc (khớp danh sách đang xem).
        OrderSummaryQuery.Totals totals;
        List<LegacyOrder> items;
        var few = OrderListFilter.HasFilters(q)
            ? await OrderListRows.Select(query, q.SortBy).Take(OrderListRows.Limit + 1).ToListAsync(ct)
            : null;
        if (few is not null && few.Count <= OrderListRows.Limit)
        {
            // Kết quả ít: 1 lượt quét đã có đủ dòng gọn → đếm, sắp xếp, phân trang ở bộ nhớ; chỉ đọc đủ cột cho đúng 1 trang.
            totals = OrderSummaryQuery.Summarize(
                few.GroupBy(r => (r.Waiting, r.NotDelivered))
                    .Select(g => new OrderSummaryQuery.Group(g.Key.Waiting, g.Key.NotDelivered, g.Count(), g.Sum(r => r.Pieces), g.Sum(r => r.Weight))),
                statuses);
            var ids = OrderListRows.Sort(few.Where(r => OrderListFilter.MatchesStatus(statuses, r.Waiting, r.NotDelivered)), q.SortBy, q.SortDir)
                .Skip((int)Math.Min(offset, int.MaxValue)).Take(size).Select(r => r.Id).ToList();
            var byId = ids.Count == 0
                ? []
                : await db.LegacyOrders.AsNoTracking().Where(o => ids.Contains(o.Id)).Select(LegacyOrderProjections.List).ToDictionaryAsync(o => o.Id, ct);
            items = [.. ids.Where(byId.ContainsKey).Select(id => byId[id])];
        }
        else
        {
            // Không lọc / kết quả nhiều: đếm bằng 1 truy vấn gom nhóm, trang lấy bằng SQL (đơn khớp dày nên dừng sớm).
            totals = OrderSummaryQuery.Summarize(await OrderSummaryQuery.Build(query).ToListAsync(ct), statuses);
            // Không gửi truy vấn trang khi bộ lọc rỗng / trang ngoài phạm vi; tránh tràn số OFFSET.
            items = offset < totals.Total
                ? await OrderListFilter.Sort(OrderListFilter.ApplyStatus(query, q.Status, today), q.SortBy, q.SortDir)
                    .Skip((int)offset).Take(size).Select(LegacyOrderProjections.List).ToListAsync(ct)
                : [];
        }
        var total = totals.Total;
        var rows = await access.WithCustomersAsync(scope, [.. items.Select(o => (o, LegacyOrderView.ToListDto(o, today)))], ct);
        return new OrderListResponse(
            rows,
            total, page, size, (int)Math.Ceiling(total / (double)size),
            new OrderSummaryDto(totals.Counts, totals.Pieces, totals.Weight));
    }
}
