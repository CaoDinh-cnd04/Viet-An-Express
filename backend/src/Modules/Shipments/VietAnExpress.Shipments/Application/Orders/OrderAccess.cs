using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Domain;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

internal static class OrderErrors
{
    public static Error NotFound(string bill) => Error.NotFound("ORDER_NOT_FOUND", $"Không tìm thấy đơn {bill}");
    public static readonly Error DraftNotFound = Error.NotFound("DRAFT_NOT_FOUND", "Không tìm thấy đơn nháp");
    public static readonly Error DraftChanged = Error.Conflict("DRAFT_CHANGED",
        "Đơn này vừa được in hoặc xóa ở nơi khác — vui lòng tải lại danh sách");
    public static readonly Error DraftNotReady =
        Error.BusinessRule("DRAFT_NOT_READY", "Đơn chưa khai đủ thông tin — mở đơn, hoàn tất rồi bấm In");
    public static readonly Error InvalidPayload =
        Error.BusinessRule("DRAFT_INVALID_PAYLOAD", "Dữ liệu đơn nháp không đọc được — vui lòng mở đơn và lưu lại");
    public static readonly Error CustomerNotFound = Error.BusinessRule("CUSTOMER_NOT_FOUND",
        "Không tìm thấy hồ sơ khách hàng — vui lòng liên hệ nhân viên kinh doanh Việt An");
    public static readonly Error CustomerRequired = Error.Forbidden("ORDER_CUSTOMER_REQUIRED",
        "Vui lòng đăng nhập bằng tài khoản khách hàng để tạo đơn");
}

/// <summary>
/// Phạm vi đơn trong dbo.MaVanDon: <paramref name="CustomerId"/> của khách; <paramref name="StaffId"/> có giá trị thì
/// chỉ lấy đơn tài khoản con đó tạo (MaVanDon.Customer_Staff_ID).
/// </summary>
internal sealed record OrderScope(long CustomerId, long? StaffId);

/// <summary>
/// Phạm vi đơn: khách chỉ thấy / ghi đơn có CustomerID = mã khách của mình (dbo.TCustomer.CustomerID).
/// Tài khoản con không có quyền <see cref="ShipmentsPermissions.ViewAll"/> chỉ thấy đơn và nháp mình tạo.
/// </summary>
internal sealed class OrderAccess(ICurrentUser user, ICustomersApi customers)
{
    /// <summary>Chưa đăng nhập bằng tài khoản khách → không thấy đơn nào (Id không tồn tại).</summary>
    private const long NoCustomer = -1;

    public bool IsCustomer => user.CustomerId is not null;

    /// <summary>Tài khoản con đang đăng nhập (ghi vào MaVanDon.Customer_Staff_ID khi tạo đơn); null = tài khoản chính.</summary>
    public long? CreatorStaffId => user.StaffId;

    public Task<OrderScope> ScopeAsync(CancellationToken ct) =>
        Task.FromResult(new OrderScope(user.CustomerId ?? NoCustomer, user.RestrictedStaffId(ShipmentsPermissions.ViewAll)));

    public IQueryable<LegacyOrder> Apply(IQueryable<LegacyOrder> query, OrderScope scope)
    {
        query = query.Where(o => o.CustomerId == scope.CustomerId);
        return scope.StaffId is { } staffId
            ? query.Where(o => o.CustomerStaffId == staffId)
            : query;
    }

    /// <summary>
    /// Ghi người tạo vào MaVanDon.Customer_Staff_ID (cột hệ thống cũ = người liên hệ / nhân viên của khách,
    /// dbo.TCustomer_NguoiLienLac.ID): tài khoản con → ID của mình, tài khoản chính → 0 như hệ thống cũ.
    /// </summary>
    public void StampCreator(LegacyOrder order) => order.CustomerStaffId = CreatorStaffId ?? 0;

    /// <summary>Khách ghi đơn vào dbo.MaVanDon — kèm tên công ty, người liên hệ để điền người gửi.</summary>
    public async Task<Result<LegacyCustomerRef>> WriterAsync(CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return Result.Failure<LegacyCustomerRef>(OrderErrors.CustomerRequired);
        var customer = await customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure<LegacyCustomerRef>(OrderErrors.CustomerNotFound);
        return new LegacyCustomerRef(customer.Id, customer.CompanyName, customer.ContactName, customer.Phone, customer.Email);
    }
}

