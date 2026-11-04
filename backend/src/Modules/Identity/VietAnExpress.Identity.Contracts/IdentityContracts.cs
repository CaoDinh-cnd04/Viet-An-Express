namespace VietAnExpress.Identity.Contracts;

/// <summary>Quyền của khách. Tài khoản con có mọi quyền của khách trừ <see cref="AdminOnly"/> (không phân quyền riêng từng người).</summary>
public static class IdentityPermissions
{
    public const string ManageStaff = "account.staff";
    public const string MyTracking = "account.mytracking";
    /// <summary>Tự đổi mật khẩu — tài khoản con không có (admin đặt lại ở trang Tài khoản nhân viên).</summary>
    public const string ChangePassword = "account.password";
    /// <summary>Xem / đánh dấu đã đọc thông báo của Việt An (khách và tài khoản con).</summary>
    public const string Notifications = "account.notifications";
    /// <summary>Soạn, gửi, xóa thông báo cho khách — chỉ quản trị Việt An.</summary>
    public const string AdminNotifications = "admin.notifications";
    /// <summary>Hỗ trợ &amp; sự cố: gửi sự cố vận đơn / yêu cầu hỗ trợ / góp ý, xem / trả lời yêu cầu của mình (khách và tài khoản con).</summary>
    public const string SupportRequests = "account.troubles";
    /// <summary>Xem, trả lời, đổi trạng thái yêu cầu hỗ trợ &amp; sự cố của mọi khách — chỉ quản trị Việt An (vai trò va_admin).</summary>
    public const string AdminSupportRequests = "admin.troubles";
    /// <summary>Nhân viên Việt An (dbo.USERS) đăng nhập portal: chọn / đổi khách để làm thay.</summary>
    public const string ActForCustomer = "vietan.act";
    /// <summary>Nhân viên Việt An: trang Hỗ trợ &amp; góp ý thành trang quản lý — xem, trả lời, đổi trạng thái yêu cầu của khách mình phụ trách.</summary>
    public const string ManageSupportRequests = "vietan.troubles";

    /// <summary>Chỉ tài khoản chính có: quản lý tài khoản con, MyTracking, tự đổi mật khẩu.</summary>
    public static readonly IReadOnlySet<string> AdminOnly = new HashSet<string>(StringComparer.Ordinal) { ManageStaff, MyTracking, ChangePassword };
}
