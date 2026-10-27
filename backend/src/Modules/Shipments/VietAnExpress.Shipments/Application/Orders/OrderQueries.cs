using System.Globalization;
using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Carriers.HubEz;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

// ---------- Danh sách đơn (GET /orders) ----------

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

/// <summary>Bộ lọc + sắp xếp dùng chung cho danh sách đơn và xuất bảng kê — 2 nơi luôn ra cùng tập đơn.</summary>
internal static class OrderListFilter
{
    /// <summary>Số giá trị tìm tối đa mỗi lần (khách dán cả cột bill).</summary>
    public const int MaxTerms = 200;

    private static readonly char[] LineBreaks = ['\n', '\r'];

    /// <summary>
    /// Ô tìm có thể chứa nhiều giá trị, mỗi giá trị 1 dòng (frontend gửi ngăn bằng ký tự xuống dòng).
    /// Bỏ trống, bỏ trùng (không phân biệt hoa thường), tối đa <see cref="MaxTerms"/>.
    /// </summary>
    public static IReadOnlyList<string> SearchTerms(string? q) =>
        (q ?? "")
            .Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();

    /// <summary>"all" hoặc danh sách "wait,fly" → các trạng thái hợp lệ; rỗng = không lọc.</summary>
    public static IReadOnlyList<string> Statuses(string? status) =>
        (status ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => LegacyOrderStatus.All.Contains(s))
            .Distinct()
            .ToList();

    /// <summary>Lọc trạng thái (chọn nhiều → khớp bất kỳ). Tách khỏi <see cref="Apply"/> vì số đếm các tab tính trước bước này.</summary>
    public static IQueryable<LegacyOrder> ApplyStatus(IQueryable<LegacyOrder> query, string? status, DateTime today)
    {
        var list = Statuses(status);
        if (list.Count == 0 || list.Count == LegacyOrderStatus.All.Length) return query;
        return query.Where(AnyOf(list.Select(s => LegacyOrderStatus.Is(s, today))));
    }

    private static readonly string[] SearchFields = ["all", "cnee", "bill", "ref", "ct"];

    /// <summary>
    /// Thẻ tìm của frontend: mỗi dòng "field:giá trị" (vd "cnee:Ms Uyen", "ct:Singapore").
    /// Dòng không có tiền tố trường hợp lệ dùng <paramref name="fallbackField"/> (searchField / link cũ).
    /// </summary>
    public static IReadOnlyList<(string Field, string Value)> SearchTags(string? q, string? fallbackField)
    {
        var fallback = SearchFields.Contains(fallbackField) ? fallbackField! : "all";
        return SearchTerms(q)
            .Select(term =>
            {
                var colon = term.IndexOf(':');
                return colon > 0 && SearchFields.Contains(term[..colon])
                    ? (Field: term[..colon], Value: term[(colon + 1)..].Trim())
                    : (Field: fallback, Value: term);
            })
            .Where(t => t.Value.Length > 0)
            .ToList();
    }

    public static IQueryable<LegacyOrder> Apply(IQueryable<LegacyOrder> query, GetOrdersQuery q)
    {
        // Cùng trường: khớp bất kỳ (OR). Khác trường: phải khớp tất cả (AND) — vd người nhận "Uyen" VÀ nước đến "Singapore".
        foreach (var group in SearchTags(q.Q, q.SearchField).GroupBy(t => t.Field))
            query = query.Where(AnyOf(group.Select(t => Match(t.Field, t.Value))));

        if (q.FromDate is { } from)
        {
            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(o => o.CreateDate >= fromDate);
        }
        if (q.ToDate is { } to)
        {
            var toDate = to.ToDateTime(TimeOnly.MinValue);
            query = query.Where(o => o.CreateDate <= toDate);
        }
        query = q.Type?.Trim().ToUpperInvariant() switch
        {
            "DOC" => query.Where(LegacyDocumentRule.IsDocument),
            "PACK" => query.Where(LegacyDocumentRule.IsNotDocument),
            _ => query
        };
        if (q.WeightFrom is { } wFrom) query = query.Where(o => o.WeightKg >= wFrom);
        if (q.WeightTo is { } wTo) query = query.Where(o => o.WeightKg <= wTo);
        return query;
    }

    /// <summary>Điều kiện khớp 1 giá trị tìm theo trường đang chọn.</summary>
    private static Expression<Func<LegacyOrder, bool>> Match(string? field, string term)
    {
        // Không phân biệt hoa / thường ở mọi collation: so sánh UPPER(cột) với từ khóa viết hoa.
        term = VaBillCode.StripPiece(term); // máy quét nhãn kiện gõ "số bill/số kiện"
        var like = term.ToUpperInvariant().ToLikePattern()!;
        // Gõ 1 phần số VA (vd "6010") cũng tìm ra — so khớp chuỗi số, không cần gõ đủ.
        var digits = term.All(char.IsAsciiDigit);
        return (field ?? "all") switch
        {
            "cnee" => o => EF.Functions.Like(o.ConsigneeName!.ToUpper(), like) || EF.Functions.Like(o.ConsigneeContactName!.ToUpper(), like),
            "bill" => o => (digits && o.OrderNumber != null && EF.Functions.Like(o.OrderNumber.Value.ToString(), like))
                || EF.Functions.Like(o.VaBill!.ToUpper(), like)
                || EF.Functions.Like(o.BillConnect!.ToUpper(), like),
            "ref" => o => EF.Functions.Like(o.CustomerBill!.ToUpper(), like),
            "ct" => o => EF.Functions.Like(o.ConsigneeCountry!.ToUpper(), like),
            _ => o => (digits && o.OrderNumber != null && EF.Functions.Like(o.OrderNumber.Value.ToString(), like))
                || EF.Functions.Like(o.VaBill!.ToUpper(), like)
                || EF.Functions.Like(o.BillConnect!.ToUpper(), like)
                || EF.Functions.Like(o.CustomerBill!.ToUpper(), like)
                || EF.Functions.Like(o.ConsigneeName!.ToUpper(), like)
                || EF.Functions.Like(o.ConsigneeCountry!.ToUpper(), like)
        };
    }

    /// <summary>Ghép nhiều điều kiện bằng OR thành 1 biểu thức EF dịch được sang SQL.</summary>
    internal static Expression<Func<T, bool>> AnyOf<T>(IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        var list = predicates.ToList();
        if (list.Count == 1) return list[0];
        var param = Expression.Parameter(typeof(T), "o");
        Expression? body = null;
        foreach (var p in list)
        {
            var next = new ReplaceParameter(p.Parameters[0], param).Visit(p.Body);
            body = body is null ? next : Expression.OrElse(body, next);
        }
        return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(false), param);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    public static IOrderedQueryable<LegacyOrder> Sort(IQueryable<LegacyOrder> query, string? sortBy, string? sortDir)
    {
        var asc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<LegacyOrder> By<TKey>(System.Linq.Expressions.Expression<Func<LegacyOrder, TKey>> key) =>
            asc ? query.OrderBy(key) : query.OrderByDescending(key);

        var ordered = sortBy switch
        {
            "ref" => By(o => o.CustomerBill),
            "bill" => By(o => o.OrderNumber),
            "cnee" => By(o => o.ConsigneeName),
            "ct" => By(o => o.ConsigneeCountry),
            "sent" => By(o => o.SentDate),
            "pod" => By(o => o.PodEstimate),
            "created" => By(o => o.CreateDate),
            _ => By(o => o.Id)
        };
        // Khoá phụ để phân trang ổn định.
        return asc ? ordered.ThenBy(o => o.Id) : ordered.ThenByDescending(o => o.Id);
    }
}

// ---------- Chi tiết 1 đơn + hành trình ----------

internal sealed record GetOrderQuery(string Bill) : IRequest<Result<OrderDto>>;

internal sealed record GetOrderEventsQuery(string Bill) : IRequest<Result<IReadOnlyList<OrderEventDto>>>;

internal sealed class GetOrderHandlers(ShipmentsDbContext db, OrderAccess access, TimeProvider clock, IHubEzTracking hubEz) :
    IRequestHandler<GetOrderQuery, Result<OrderDto>>,
    IRequestHandler<GetOrderEventsQuery, Result<IReadOnlyList<OrderEventDto>>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery q, CancellationToken ct)
    {
        var order = await FindAsync(q.Bill, ct);
        if (order is null) return OrderErrors.NotFound(q.Bill);

        // Kiện + dòng hàng invoice nằm ở 2 bảng chi tiết, nối theo MaVanDon.ID (cột int).
        var id = order.Id is > 0 and <= int.MaxValue ? (int)order.Id : 0;
        var packages = await db.LegacyPackageLines.AsNoTracking().Where(p => p.OrderId == id).OrderBy(p => p.Id).ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking().Where(i => i.OrderId == id).OrderBy(i => i.Id).ToListAsync(ct);
        return LegacyOrderView.ToDetailDto(order, VietnamTime.ToVietnam(clock.GetUtcNow()).Date, packages, items);
    }

    public async Task<Result<IReadOnlyList<OrderEventDto>>> Handle(GetOrderEventsQuery q, CancellationToken ct)
    {
        var order = await FindAsync(q.Bill, ct, eventsOnly: true);
        if (order is null) return Result.Failure<IReadOnlyList<OrderEventDto>>(OrderErrors.NotFound(q.Bill));
        var events = LegacyOrderView.Events(order, hideSigner: false);
        if (!hubEz.Handles(order.ServiceName, order.BillConnect)) return Result.Success(events);
        // Đơn đi HUB-EZ: thêm hành trình của HUB-EZ theo mã hãng (HUB-EZ lỗi → giữ hành trình cũ).
        var tracks = await hubEz.TrackAsync([order.BillConnect!], ct);
        return Result.Success(HubEzEvents.Merge(events, tracks.GetValueOrDefault(order.BillConnect!.Trim()), !string.IsNullOrWhiteSpace(order.Pod)));
    }

    /// <summary>Tìm theo số VA hoặc mã hãng, trong phạm vi khách của người dùng.</summary>
    private async Task<LegacyOrder?> FindAsync(string bill, CancellationToken ct, bool eventsOnly = false)
    {
        var code = bill.Trim();
        var number = VaBillCode.Number(code);
        var query = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct));
        // Mã hãng không phải số không được khớp nhầm tất cả dòng OrderNumber NULL.
        query = number is { } parsed
            ? query.Where(o => o.OrderNumber == parsed || o.BillConnect == code)
            : query.Where(o => o.BillConnect == code);
        query = query.OrderByDescending(o => o.Id);
        if (eventsOnly) query = query.Select(LegacyOrderProjections.Events);
        return await query.FirstOrDefaultAsync(ct);
    }
}
