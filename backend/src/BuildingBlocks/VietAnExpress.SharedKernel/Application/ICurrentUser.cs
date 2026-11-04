namespace VietAnExpress.SharedKernel.Application;

/// <summary>
/// Người dùng của request hiện tại (đọc từ JWT). Portal chỉ cho khách hàng đăng nhập (tài khoản ở dbo.TCustomer).
/// Ngoài HTTP (job, startup) thì chưa xác thực.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserName { get; }

    /// <summary>dbo.TCustomer.CustomerID của khách đang đăng nhập — mọi dữ liệu phải lọc theo khách này.</summary>
    long? CustomerId { get; }

    /// <summary>Có khi đăng nhập bằng tài khoản con (nhân viên); null = tài khoản chính (admin) của khách.</summary>
    long? StaffId { get; }

    /// <summary>Có khi nhân viên Việt An (dbo.USERS.ID) đang làm thay khách <see cref="CustomerId"/>; null = chính khách đăng nhập.</summary>
    int? VietAnUserId => null;

    bool HasPermission(string permission);
}

/// <summary>Quy tắc phạm vi dữ liệu theo loại tài khoản — dùng chung cho mọi module.</summary>
public static class CurrentUserScope
{
    /// <summary>
    /// Tài khoản con chỉ thấy bản ghi mình tạo → trả StaffID để lọc; tài khoản chính → null (thấy mọi bản ghi của khách).
    /// </summary>
    public static long? RestrictedStaffId(this ICurrentUser user) => user.StaffId;

    /// <summary>CustomerID khi đang dùng tài khoản chính (admin) của khách; tài khoản con / chưa đăng nhập → null.</summary>
    public static long? MainAccountCustomerId(this ICurrentUser user) => user.StaffId is null ? user.CustomerId : null;
}
