using System.Globalization;
using VietAnExpress.Shipments.OrderData;

namespace VietAnExpress.Shipments.Orders;

/// <summary>Dòng gọn của 1 đơn khớp bộ lọc: đủ để đếm tab, cộng kiện / cân, sắp xếp và phân trang ở bộ nhớ.</summary>
internal sealed record OrderListRow(long Id, bool Waiting, bool NotDelivered, int Pieces, decimal Weight, long? Number, DateTime? Date, string? Text);

/// <summary>
/// Kết quả lọc ít (tìm bill / Ref / người nhận…, ≤ <see cref="Limit"/> đơn): lấy dòng gọn trong MỘT lượt quét rồi đếm + sắp xếp + phân trang
/// ở bộ nhớ, sau đó chỉ đọc đủ cột cho đúng 1 trang theo ID. Trước 06/11/2026 phải quét 2 lượt (đếm, rồi tìm trang) — với 300k đơn
/// mỗi lượt là phần đắt nhất. Kết quả nhiều hơn → <see cref="GetOrdersHandler"/> quay về đếm bằng SQL + lấy trang bằng SQL.
/// </summary>
internal static class OrderListRows
{
    public const int Limit = 5000;

    /// <summary>Cờ trạng thái giống hệt <see cref="LegacyOrderStatus.Is"/>; chỉ đọc thêm 1 cột sắp xếp đang chọn.</summary>
    public static IQueryable<OrderListRow> Select(IQueryable<LegacyOrder> query, string? sortBy) => sortBy switch
    {
        "ref" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, null, o.CustomerBill)),
        "cnee" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, null, o.ConsigneeName)),
        "ct" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, null, o.ConsigneeCountry)),
        "bill" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, o.OrderNumber, null, null)),
        "sent" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, o.SentDate, null)),
        "pod" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, o.PodEstimate, null)),
        "created" => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, o.CreateDate, null)),
        _ => query.Select(o => new OrderListRow(o.Id, o.SentDate == null, o.Pod == null || o.Pod == "", o.Pieces ?? 0, o.WeightKg ?? 0m, null, null, null))
    };

    /// <summary>Cùng thứ tự với <see cref="OrderListFilter.Sort"/> (NULL đứng đầu khi tăng dần như SQL Server, khóa phụ ID). Hàm thuần — có test.</summary>
    public static IEnumerable<OrderListRow> Sort(IEnumerable<OrderListRow> rows, string? sortBy, string? sortDir)
    {
        var asc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedEnumerable<OrderListRow> By<TKey>(Func<OrderListRow, TKey> key, IComparer<TKey>? comparer = null) =>
            asc ? rows.OrderBy(key, comparer) : rows.OrderByDescending(key, comparer);

        var ordered = sortBy switch
        {
            "ref" or "cnee" or "ct" => By(r => r.Text, TextOrder.Instance),
            "bill" => By(r => r.Number),
            "sent" or "pod" or "created" => By(r => r.Date),
            _ => By(r => r.Id)
        };
        return asc ? ordered.ThenBy(r => r.Id) : ordered.ThenByDescending(r => r.Id);
    }

    /// <summary>Gần với collation cột (không phân biệt hoa thường, bỏ khoảng trắng cuối như SQL Server).</summary>
    private sealed class TextOrder : IComparer<string?>
    {
        public static readonly TextOrder Instance = new();
        private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

        int IComparer<string?>.Compare(string? x, string? y) =>
            x is null ? (y is null ? 0 : -1) : y is null ? 1 : Compare.Compare(x.TrimEnd(' '), y.TrimEnd(' '), CompareOptions.IgnoreCase);
    }
}
