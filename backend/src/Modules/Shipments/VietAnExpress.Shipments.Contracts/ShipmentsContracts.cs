namespace VietAnExpress.Shipments.Contracts;

public static class ShipmentsPermissions
{
    public const string View = "shipments.view";
    public const string Create = "shipments.create";
    public const string Update = "shipments.update";
    public const string IssueBill = "shipments.issue-bill";
    /// <summary>Nhân viên Việt An: xem đơn của mọi khách mình phụ trách (TCustomer.Staff_ID; vai trò Administrator = mọi khách) — có cả khi chưa chọn khách.</summary>
    public const string ManagedOrders = "vietan.orders";
}

/// <summary>Cổng gọi đồng bộ vào module Shipments cho module khác (vận đơn ở dbo.MaVanDon).</summary>
public interface IShipmentsApi
{
    /// <summary>
    /// Vận đơn của khách theo mã bill (VA Bill, số VA 7 chữ số hoặc mã hãng). Không có / thuộc khách khác → null.
    /// </summary>
    Task<BillSummary?> FindBillAsync(long customerId, string bill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khách có đơn (cả nháp) trong <see cref="CustomerOrderActivity.WindowDays"/> ngày gần đây, mới tạo đơn trước — để trang chọn khách
    /// của nhân viên Việt An đưa khách hay gửi lên đầu. <paramref name="customerIds"/> null = mọi khách. Tối đa <paramref name="take"/> dòng.
    /// </summary>
    Task<IReadOnlyList<CustomerOrderActivity>> GetCustomerActivityAsync(IReadOnlyCollection<long>? customerIds, int take,
        CancellationToken cancellationToken = default);
}

/// <summary>Đơn gần đây của 1 khách: <see cref="LastOrderId"/> = MaVanDon.ID lớn nhất (đơn tạo sau cùng), <see cref="RecentOrders"/> = số đơn trong cửa sổ.</summary>
public sealed record CustomerOrderActivity(long CustomerId, DateTime? LastOrderDate, long LastOrderId, int RecentOrders)
{
    public const int WindowDays = 90;
}

/// <summary>Thông tin vận đơn đủ dùng cho module khác: mã bill hiển thị, người nhận, quốc gia.</summary>
public sealed record BillSummary(string Bill, string? ConsigneeName, string? ConsigneeCountry);
