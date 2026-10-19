namespace VietAnExpress.Identity.Contracts;

/// <summary>Quyền chỉ tài khoản chính (admin) của khách có — không cấp được cho tài khoản con.</summary>
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
    public const string Troubles = "account.troubles";
    /// <summary>Xem, trả lời, đổi trạng thái yêu cầu hỗ trợ &amp; sự cố của mọi khách — chỉ quản trị Việt An (vai trò va_admin).</summary>
    public const string AdminTroubles = "admin.troubles";
    public const string AdminAnalytics = "admin.analytics";

    public static readonly IReadOnlySet<string> AdminOnly = new HashSet<string>(StringComparer.Ordinal) { ManageStaff, MyTracking, ChangePassword };

    /// <summary>Tài khoản con luôn có (không cần tài khoản chính chọn, không hiện trong danh sách quyền để chọn).</summary>
    public static readonly IReadOnlySet<string> StaffAlways = new HashSet<string>(StringComparer.Ordinal) { Notifications, Troubles };
}
