using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;

namespace VietAnExpress.Shipments.Orders;

/// <summary>
/// Số đơn từng tab + tổng kiện / cân bằng MỘT truy vấn SQL: gom đơn theo 2 cờ trạng thái (chưa đi? · chưa phát?) → tối đa 4 dòng,
/// rồi cộng ở bộ nhớ cho mọi tổ hợp tab đang chọn (<see cref="OrderListFilter.MatchesStatus"/>) — không phải đếm lại khi chọn 2 tab
/// (trước 06/11/2026 phải thêm 1 lượt quét). Cờ trạng thái giống hệt <see cref="LegacyOrderStatus.Is"/>.
/// </summary>
internal static class OrderSummaryQuery
{
    /// <summary>1 nhóm đơn cùng trạng thái: chưa đi (Sent_Date trống) / chưa phát (POD trống).</summary>
    internal sealed record Group(bool Waiting, bool NotDelivered, int Count, int Pieces, decimal Weight);

    /// <summary>Kết quả cho danh sách: số đơn từng tab (tính TRƯỚC lọc tab) + tổng đơn / kiện / cân của tab đang chọn.</summary>
    internal sealed record Totals(Dictionary<string, int> Counts, int Total, int Pieces, decimal Weight);

    public static IQueryable<Group> Build(IQueryable<LegacyOrder> orders) =>
        orders
            .GroupBy(o => new { Waiting = o.SentDate == null, NotDelivered = o.Pod == null || o.Pod == "" })
            .Select(g => new Group(g.Key.Waiting, g.Key.NotDelivered, g.Count(), g.Sum(o => o.Pieces ?? 0), g.Sum(o => o.WeightKg ?? 0m)));

    /// <summary>Cộng các nhóm: số từng tab + tổng của các nhóm khớp tab đang chọn. Hàm thuần — có test.</summary>
    public static Totals Summarize(IEnumerable<Group> groups, IReadOnlyList<string> statuses)
    {
        var list = groups.ToList();
        var counts = new Dictionary<string, int>
        {
            ["all"] = list.Sum(g => g.Count),
            [LegacyOrderStatus.Waiting] = list.Where(g => g.Waiting).Sum(g => g.Count),
            [LegacyOrderStatus.InTransit] = list.Where(g => !g.Waiting).Sum(g => g.Count),
            [LegacyOrderStatus.NotDelivered] = list.Where(g => g.NotDelivered).Sum(g => g.Count),
            [LegacyOrderStatus.Delivered] = list.Where(g => !g.NotDelivered).Sum(g => g.Count)
        };
        var picked = list.Where(g => OrderListFilter.MatchesStatus(statuses, g.Waiting, g.NotDelivered)).ToList();
        return new Totals(counts, picked.Sum(g => g.Count), picked.Sum(g => g.Pieces), picked.Sum(g => g.Weight));
    }
}
