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
    string? SortDir) : IRequest<OrderListResponse>;

internal sealed class GetOrdersHandler(ShipmentsDbContext db, OrderAccess access, TimeProvider clock)
    : IRequestHandler<GetOrdersQuery, OrderListResponse>
{
    public async Task<OrderListResponse> Handle(GetOrdersQuery q, CancellationToken ct)
    {
        var today = VietnamTime.ToVietnam(clock.GetUtcNow()).Date;
        var query = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct));
        query = OrderListFilter.Apply(query, q);

        // Số đếm theo trạng thái tính trên kết quả lọc TRỪ lọc trạng thái — để số trên các tab luôn đúng;
        // tổng kiện / cân thì theo đúng trạng thái đang lọc (khớp danh sách đang xem).
        var totals = await OrderSummaryQuery.Build(query, today).FirstOrDefaultAsync(ct) ?? OrderSummaryQuery.Totals.Empty;
        var counts = totals.Counts();
        var statuses = OrderListFilter.Statuses(q.Status);
        query = OrderListFilter.ApplyStatus(query, q.Status, today);

        int total;
        int pieces;
        decimal weight;
        if (OrderSummaryQuery.Totals.NeedsRecount(statuses))
        {
            // Nhóm trạng thái trùng nhau (vd đã đi + đã phát) → đếm lại trên đúng tập đang xem, không cộng số từng nhóm.
            var picked = await OrderSummaryQuery.Build(query, today).FirstOrDefaultAsync(ct) ?? OrderSummaryQuery.Totals.Empty;
            (total, pieces, weight) = (picked.All, picked.Pieces, picked.Weight);
        }
        else
        {
            total = statuses.Count == 1 ? counts[statuses.First()] : totals.All;
            (pieces, weight) = totals.For(statuses);
        }

        var (page, size) = Paging.Normalize(q.Page, q.PageSize);
        var offset = (long)(page - 1) * size;
        // Không gửi truy vấn trang khi bộ lọc rỗng / trang ngoài phạm vi; tránh tràn số OFFSET.
        var items = offset < total
            ? await OrderListFilter.Sort(query, q.SortBy, q.SortDir)
                .Skip((int)offset).Take(size).Select(LegacyOrderProjections.List).ToListAsync(ct)
            : [];
        return new OrderListResponse(
            items.Select(o => LegacyOrderView.ToListDto(o, today)).ToList(),
            total, page, size, (int)Math.Ceiling(total / (double)size),
            new OrderSummaryDto(counts, pieces, weight));
    }
}
