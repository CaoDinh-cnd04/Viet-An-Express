namespace VietAnExpress.Identity.Contracts;

/// <summary>Quyền chỉ tài khoản chính (admin) của khách có — không cấp được cho tài khoản con.</summary>
public static class IdentityPermissions
{
    public const string ManageStaff = "account.staff";
    public const string MyTracking = "account.mytracking";
    /// <summary>Tự đổi mật khẩu — tài khoản con không có (admin đặt lại ở trang Tài khoản nhân viên).</summary>
    public const string ChangePassword = "account.password";
    /// <summary>Gửi góp ý (khách và tài khoản con).</summary>
    public const string Feedback = "account.feedback";
    /// <summary>Xem góp ý của mọi khách — chỉ quản trị Việt An (vai trò va_admin), khách không bao giờ có.</summary>
    public const string AdminFeedback = "admin.feedback";
    /// <summary>Xem / đánh dấu đã đọc thông báo của Việt An (khách và tài khoản con).</summary>
    public const string Notifications = "account.notifications";
    /// <summary>Soạn, gửi, xóa thông báo cho khách — chỉ quản trị Việt An.</summary>
    public const string AdminNotifications = "admin.notifications";
    /// <summary>Báo sự cố vận đơn, xem / trả lời sự cố của mình (khách và tài khoản con).</summary>
    public const string Troubles = "account.troubles";
    /// <summary>Xem, trả lời, đổi trạng thái sự cố của mọi khách — chỉ quản trị Việt An.</summary>
    public const string AdminTroubles = "admin.troubles";
    public const string AdminAnalytics = "admin.analytics";
    /// <summary>Gửi yêu cầu sửa thông tin người gửi — chỉ tài khoản chính.</summary>
    public const string SenderInfo = "account.sender-info";
    /// <summary>Duyệt / từ chối yêu cầu sửa thông tin người gửi — chỉ quản trị Việt An.</summary>
    public const string AdminSenderInfo = "admin.sender-info";

    public static readonly IReadOnlySet<string> AdminOnly = new HashSet<string>(StringComparer.Ordinal) { ManageStaff, MyTracking, ChangePassword, SenderInfo };

    /// <summary>Tài khoản con luôn có (không cần tài khoản chính chọn, không hiện trong danh sách quyền để chọn).</summary>
    public static readonly IReadOnlySet<string> StaffAlways = new HashSet<string>(StringComparer.Ordinal) { Feedback, Notifications, Troubles };
}
