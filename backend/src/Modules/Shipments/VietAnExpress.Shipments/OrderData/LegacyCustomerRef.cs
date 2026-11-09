using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>
/// Khách sở hữu đơn, theo mã của hệ thống cũ (dbo.TCustomer). <see cref="IsNew"/> = khách mới nhân viên Việt An tạo trên portal
/// (hồ sơ còn trống) — tên khách trên đơn lấy theo người gửi, lưu đơn thì ghi người gửi vào hồ sơ.
/// </summary>
internal sealed record LegacyCustomerRef(long LegacyCustomerId, string CompanyName, string? ContactName, string? Phone, string? Email,
    bool IsNew = false);
