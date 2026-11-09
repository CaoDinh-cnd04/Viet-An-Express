using VietAnExpress.Customers.Contracts;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Thứ tự trang chọn khách (06/11/2026 người dùng chốt): khách mới (chờ CS hoàn thiện) đứng đầu, rồi khách mới tạo đơn gần đây
/// (ngày tạo đơn gần nhất), cùng ngày thì khách tạo nhiều đơn hơn (<see cref="CustomerOrderActivity.WindowDays"/> ngày) lên trước,
/// sau cùng là khách chưa có đơn gần đây theo thứ tự backend trả (mã khách). Hàm thuần — có test.
/// </summary>
internal static class ActableCustomerRanking
{
    public static IReadOnlyList<CustomerSummary> Order(IEnumerable<CustomerSummary> candidates, IReadOnlyCollection<CustomerOrderActivity> activity, int take)
    {
        var byId = activity.GroupBy(a => a.CustomerId).ToDictionary(g => g.Key, g => g.First());
        return candidates
            .DistinctBy(c => c.Id)
            .Select((c, i) => (Customer: c, Index: i, Activity: byId.GetValueOrDefault(c.Id)))
            .OrderByDescending(x => x.Customer.IsNew)
            .ThenByDescending(x => x.Activity is not null)
            .ThenByDescending(x => x.Activity?.LastOrderDate?.Date)
            .ThenByDescending(x => x.Activity?.RecentOrders ?? 0)
            .ThenByDescending(x => x.Activity?.LastOrderId ?? 0)
            .ThenBy(x => x.Index)
            .Take(take)
            .Select(x => x.Customer)
            .ToList();
    }
}
