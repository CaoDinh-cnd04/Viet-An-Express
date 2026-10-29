
namespace VietAnExpress.Identity.Notifications;

/// <summary>Trạng thái đăng (dbo.TThongBao.Trang_Thai).</summary>
internal static class NotificationStatus
{
    public const int Draft = 0;
    /// <summary>Đã đăng — khách thấy từ <see cref="Notification.PublishAt"/> tới <see cref="Notification.ExpiresAt"/>.</summary>
    public const int Published = 1;
    /// <summary>Đã gỡ khỏi portal của khách (vẫn xem lại được ở quản trị).</summary>
    public const int Removed = 2;
}

/// <summary>Trạng thái gửi email của 1 thông báo.</summary>
internal static class NotificationEmailStatus
{
    /// <summary>Không chọn gửi email.</summary>
    public const string None = "none";
    /// <summary>Chờ gửi: nháp có chọn gửi email, hoặc thông báo hẹn giờ chưa tới giờ đăng.</summary>
    public const string Scheduled = "scheduled";
    /// <summary>Đã xếp hàng, đang gửi ở nền.</summary>
    public const string Sending = "sending";
    public const string Sent = "sent";
    /// <summary>Gửi được một phần (có lô lỗi hoặc vượt giới hạn người nhận).</summary>
    public const string Partial = "partial";
    public const string Failed = "failed";
    /// <summary>Không có khách nào có email hợp lệ.</summary>
    public const string NoRecipients = "no_recipients";

    public const int MaxLength = 20;

    /// <summary>Email đã (hoặc đang) gửi — sửa thông báo không gửi lại.</summary>
    public static bool Started(string status) => status is not (None or Scheduled);
}

/// <summary>Đối tượng nhận thông báo quản trị đăng (dbo.TThongBao.Doi_Tuong).</summary>
internal static class NotificationAudience
{
    /// <summary>Mọi khách có tài khoản portal.</summary>
    public const string All = "all";
    /// <summary>Khách có <c>TCustomer.CustomerTypeID</c> thuộc <see cref="Notification.CustomerTypes"/>.</summary>
    public const string Types = "type";
    /// <summary>Khách chỉ định ở cột dbo.TThongBao.Khach_Nhan.</summary>
    public const string Customers = "pick";

    public const int MaxLength = 10;

    public static bool IsValid(string? value) => value is All or Types or Customers;
}
