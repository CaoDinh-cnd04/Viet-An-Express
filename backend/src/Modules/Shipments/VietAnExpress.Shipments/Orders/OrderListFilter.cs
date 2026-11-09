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

    /// <summary>2 nhóm trạng thái chồng nhau: hành trình (chưa đi / đã đi) và giao hàng (chưa phát / đã phát).</summary>
    private static readonly string[][] StatusGroups =
    [
        [LegacyOrderStatus.Waiting, LegacyOrderStatus.InTransit],
        [LegacyOrderStatus.NotDelivered, LegacyOrderStatus.Delivered]
    ];

    /// <summary>
    /// Lọc trạng thái, chọn được nhiều: trong 1 nhóm khớp bất kỳ (OR), giữa 2 nhóm phải khớp cả hai (AND) —
    /// vd "Đã đi" + "Chưa phát" = hàng đang trên đường (trước đây OR nên ra gần hết đơn). Chọn đủ 2 ô của 1 nhóm = nhóm đó không lọc.
    /// Tách khỏi <see cref="Apply"/> vì số đếm các tab tính trước bước này.
    /// </summary>
    public static IQueryable<LegacyOrder> ApplyStatus(IQueryable<LegacyOrder> query, string? status, DateTime today)
    {
        var list = Statuses(status);
        foreach (var group in StatusGroups)
        {
            var picked = group.Where(list.Contains).ToList();
            if (picked.Count is 0 || picked.Count == group.Length) continue;
            query = query.Where(AnyOf(picked.Select(s => LegacyOrderStatus.Is(s, today))));
        }
        return query;
    }

    /// <summary>
    /// Cùng luật với <see cref="ApplyStatus"/> nhưng tính trên 2 cờ trạng thái của đơn (đã có ở bộ nhớ):
    /// trong 1 nhóm khớp bất kỳ, giữa 2 nhóm khớp cả hai, chọn đủ / không chọn ô nào của nhóm = nhóm đó không lọc. Hàm thuần — có test.
    /// </summary>
    public static bool MatchesStatus(IReadOnlyList<string> statuses, bool waiting, bool notDelivered)
    {
        foreach (var group in StatusGroups)
        {
            var picked = group.Where(statuses.Contains).ToList();
            if (picked.Count is 0 || picked.Count == group.Length) continue;
            var ok = picked.Any(s => s switch
            {
                LegacyOrderStatus.Waiting => waiting,
                LegacyOrderStatus.InTransit => !waiting,
                LegacyOrderStatus.NotDelivered => notDelivered,
                LegacyOrderStatus.Delivered => !notDelivered,
                _ => false
            });
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>Có điều kiện lọc nào ngoài tab trạng thái (tìm, ngày, loại, cân) — dùng chọn cách truy vấn ở <see cref="GetOrdersHandler"/>.</summary>
    public static bool HasFilters(GetOrdersQuery q) =>
        SearchTags(q.Q, q.SearchField).Count > 0 || q.FromDate is not null || q.ToDate is not null
        || q.Type?.Trim().ToUpperInvariant() is "DOC" or "PACK" || q.WeightFrom is not null || q.WeightTo is not null;

    /// <summary>"kh" = khách của đơn — trang "Đơn khách phụ trách" của nhân viên Việt An.</summary>
    private static readonly string[] SearchFields = ["all", "cnee", "bill", "ref", "ct", "svc", CustomerField];

    public const string CustomerField = "kh";

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

    /// <param name="customerMatches">
    /// Trang đơn khách phụ trách: từ khóa thẻ "kh" / "Tất cả" → CustomerID khớp mã / tên / SĐT / tên đăng nhập khách (tra ở module Customers,
    /// <see cref="OrderAccess.CustomerMatchesAsync"/>); thiếu thì chỉ so tên khách ghi trên đơn (MaVanDon.CustomerName).
    /// </param>
    public static IQueryable<LegacyOrder> Apply(IQueryable<LegacyOrder> query, GetOrdersQuery q,
        IReadOnlyDictionary<string, IReadOnlyList<long>>? customerMatches = null)
    {
        // Cùng trường: khớp bất kỳ (OR). Khác trường: phải khớp tất cả (AND) — vd người nhận "Uyen" VÀ nước đến "Singapore".
        foreach (var group in SearchTags(q.Q, q.SearchField).GroupBy(t => t.Field))
        {
            var values = group.Select(t => t.Value).ToList();
            query = group.Key == "bill" && values.Count > 1
                ? ExactBills(query, values)
                : query.Where(AnyOf(group.Select(t => Match(t.Field, t.Value, customerMatches?.GetValueOrDefault(t.Value) ?? [], q.Managed))));
        }

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

    /// <summary>
    /// Điều kiện khớp 1 giá trị tìm theo trường đang chọn. "Tất cả" = đúng các trường của ô chọn: tên khách hàng (trang đơn khách phụ trách),
    /// người nhận (công ty, người liên hệ), VA Bill (số bill, VA_Bill, mã hãng), số tham chiếu, nước đến, dịch vụ.
    /// (06/11/2026: bỏ SĐT / email / địa chỉ / mã bưu chính / thành phố / bang / tên hàng khỏi "Tất cả" — 19 cột trên 300k đơn mất 12–28 giây;
    /// các cột còn lại đều nằm trong index IX_MaVanDon_Portal_Customer.)
    /// Không phân biệt hoa / thường và dấu tiếng Việt (kể cả đ / Đ): "xoai say" khớp "Xoài sấy" — xem <see cref="OrderTextSearch"/>.
    /// </summary>
    /// <param name="customerIds">Khách khớp từ khóa (chỉ trang đơn khách phụ trách) — dùng cho "kh" và "Tất cả".</param>
    /// <param name="managed">Trang đơn khách phụ trách: "Tất cả" tìm cả khách của đơn (trang 1 khách thì tên khách trên đơn là chính mình — bỏ qua).</param>
    private static Expression<Func<LegacyOrder, bool>> Match(string? field, string term, IReadOnlyList<long> customerIds, bool managed)
    {
        term = VaBillCode.StripPiece(term); // máy quét nhãn kiện gõ "số bill/số kiện"
        var t = OrderTextSearch.Term.Of(term);
        // Dịch vụ lưu "Hãng|Tuyến", bảng hiện "Hãng - Tuyến": gõ kiểu nào cũng ra.
        var service = OrderTextSearch.Term.Of(term.Replace(" - ", "|"));
        // Gõ 1 phần số VA (vd "6010") cũng tìm ra — so khớp chuỗi số, không cần gõ đủ.
        var digits = term.All(char.IsAsciiDigit);

        Expression<Func<LegacyOrder, bool>> Cnee() => AnyOf<LegacyOrder>([
            OrderTextSearch.Text(o => o.ConsigneeName, t), OrderTextSearch.Text(o => o.ConsigneeContactName, t)]);
        Expression<Func<LegacyOrder, bool>> Bill() => AnyOf<LegacyOrder>([
            .. digits ? [OrderTextSearch.Number(t)] : Array.Empty<Expression<Func<LegacyOrder, bool>>>(),
            OrderTextSearch.Code(o => o.VaBill, t), OrderTextSearch.Code(o => o.BillConnect, t)]);
        Expression<Func<LegacyOrder, bool>> Ref() => OrderTextSearch.Code(o => o.CustomerBill, t);
        Expression<Func<LegacyOrder, bool>> Country() => OrderTextSearch.Text(o => o.ConsigneeCountry, t);
        // Dịch vụ lưu dạng "Hãng|Tuyến" (vd "DHL|Singapore", "Chuyên tuyến|Taiwan") — gõ "DHL" hay "Chuyen tuyen" đều ra.
        Expression<Func<LegacyOrder, bool>> Service() => OrderTextSearch.Text(o => o.ServiceName, service);
        Expression<Func<LegacyOrder, bool>> Customer() => AnyOf<LegacyOrder>([
            o => o.CustomerId != null && customerIds.Contains(o.CustomerId.Value), OrderTextSearch.Text(o => o.CustomerName, t)]);

        return (field ?? "all") switch
        {
            "cnee" => Cnee(),
            "bill" => Bill(),
            "ref" => Ref(),
            "ct" => Country(),
            "svc" => Service(),
            CustomerField => Customer(),
            _ => AnyOf<LegacyOrder>([Bill(), Ref(), Cnee(), Country(), Service(),
                .. managed ? [Customer()] : Array.Empty<Expression<Func<LegacyOrder, bool>>>()])
        };
    }

    /// <summary>
    /// Dán nhiều bill (cả cột Excel, tối đa <see cref="MaxTerms"/>): so khớp ĐÚNG mã thay vì "chứa" từng mã — 200 mã × LIKE trên 300k đơn rất chậm.
    /// Số VA (6003585, VAHCM6003585US, 6003585/2) → OrderNumber IN (...); mã hãng / mã VA_Bill → Bill_Connect / VA_Bill IN (...).
    /// Gõ 1 giá trị vẫn tìm 1 phần như cũ (<see cref="Match"/>).
    /// </summary>
    internal static IQueryable<LegacyOrder> ExactBills(IQueryable<LegacyOrder> query, IReadOnlyList<string> values)
    {
        var (numbers, codes, others) = ExactBillKeys(values);
        // Mỗi nhánh tra thẳng index (IX_MaVanDon_OrderNumber / IX_MaVanDon_BillConnect) rồi gộp ID — không so từng mã trên từng đơn
        // (50 mã × 300k đơn mất ~5 giây). Mã VA (VAHCM6003585US) đã đổi ra số bill nên VA_Bill chỉ so mã không đọc được số.
        var ids = query.Where(o => o.OrderNumber != null && numbers.Contains(o.OrderNumber.Value)).Select(o => o.Id)
            .Union(query.Where(o => o.BillConnect != null && codes.Contains(o.BillConnect)).Select(o => o.Id));
        if (others.Count > 0) ids = ids.Union(query.Where(o => o.VaBill != null && others.Contains(o.VaBill)).Select(o => o.Id));
        return query.Where(o => ids.Contains(o.Id));
    }

    /// <summary>
    /// Dán nhiều bill → (số bill VA, mọi mã để so mã hãng, mã không đọc ra số để so VA_Bill). Số VA nhận "6003585", "VAHCM6003585US",
    /// "6003585/2" (mã vạch nhãn kiện); mã hãng toàn số (DHL) vừa so số bill vừa so mã hãng. Hàm thuần — có test.
    /// </summary>
    internal static (List<long> Numbers, List<string> Codes, List<string> Others) ExactBillKeys(IReadOnlyList<string> values)
    {
        var codes = values.Select(v => VaBillCode.StripPiece(v)).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var numbers = codes.Select(VaBillCode.Number).OfType<long>().Distinct().ToList();
        return (numbers, codes, codes.Where(c => VaBillCode.Number(c) is null).ToList());
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
