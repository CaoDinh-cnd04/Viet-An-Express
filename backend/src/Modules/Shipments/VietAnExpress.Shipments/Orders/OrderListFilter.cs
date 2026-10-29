using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Orders;

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

    private static readonly string[] SearchFields = ["all", "cnee", "bill", "ref", "ct", "svc"];

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
            // Dịch vụ lưu dạng "Hãng|Tuyến" (vd "DHL|Singapore", "Chuyên tuyến|Taiwan") — gõ "DHL" hay "Chuyên tuyến" đều ra.
            "svc" => o => EF.Functions.Like(o.ServiceName!.ToUpper(), like),
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
