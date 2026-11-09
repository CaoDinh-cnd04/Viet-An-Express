using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.NewCustomers;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.Shipments.Contracts;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>Trang chọn khách: khách mới, rồi khách mới tạo đơn / tạo nhiều đơn lên đầu.</summary>
public class ActableCustomerRankingTests
{
    private static CustomerSummary C(long id, bool isNew = false) => new(id, $"K{id}", $"Khách {id}", null, null, null, null, null, IsNew: isNew);

    [Fact]
    public void Khach_moi_roi_khach_moi_tao_don_roi_khach_nhieu_don_len_dau()
    {
        var day1 = new DateTime(2026, 11, 5);
        var day2 = new DateTime(2026, 11, 6);
        IReadOnlyCollection<CustomerOrderActivity> activity =
        [
            new(1, day1, 100, 30), // nhiều đơn nhưng ngày cũ hơn
            new(2, day2, 90, 2),
            new(3, day2, 95, 5),   // cùng ngày với 2, nhiều đơn hơn
        ];
        var order = ActableCustomerRanking.Order([C(9), C(1), C(2), C(3), C(7, isNew: true), C(2)], activity, 10);
        Assert.Equal([7L, 3, 2, 1, 9], order.Select(c => c.Id));
        Assert.Equal(2, ActableCustomerRanking.Order([C(9), C(1), C(2)], activity, 2).Count);
    }

    [Fact]
    public void Khach_moi_con_phu_trach_la_nguoi_tao_la_chua_gan()
    {
        Assert.True(NewCustomerRules.ManagerPending(NewCustomerRules.Marker, 9, 9));
        Assert.True(NewCustomerRules.ManagerPending(NewCustomerRules.Marker, 0, 9));
        Assert.False(NewCustomerRules.ManagerPending(NewCustomerRules.Marker, 7, 9));
        Assert.False(NewCustomerRules.ManagerPending("Tự tìm", 9, 9));
    }
}
