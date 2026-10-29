using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal static class NotificationErrors
{
    public static readonly Error TitleRequired = Error.Validation("NOTIFICATION_TITLE", $"Nhập tiêu đề (tối đa {Notification.TitleMaxLength} ký tự)");
    public static readonly Error BodyRequired = Error.Validation("NOTIFICATION_BODY", $"Nhập nội dung (tối đa {Notification.BodyMaxLength} ký tự)");
    public static readonly Error Link = Error.Validation("NOTIFICATION_LINK",
        $"Đường link phải bắt đầu bằng http:// hoặc https:// (tối đa {Notification.LinkMaxLength} ký tự)");
    public static readonly Error ItemsRequired = Error.Validation("NOTIFICATION_ITEMS", $"Thông báo cần 1 – {NotificationRules.MaxItems} tin");
    public static readonly Error ImageNotFound = Error.Validation("NOTIFICATION_IMAGE", "Có ảnh không còn trên máy chủ — bỏ ảnh đó và tải lên lại");
    public static readonly Error Audience = Error.Validation("NOTIFICATION_AUDIENCE", "Chọn đối tượng nhận: mọi khách, theo loại khách hoặc chọn khách");
    public static readonly Error TypesRequired = Error.Validation("NOTIFICATION_TYPES", "Chọn ít nhất 1 loại khách");
    public static readonly Error CustomersRequired = Error.Validation("NOTIFICATION_CUSTOMERS",
        $"Nhập mã khách nhận thông báo (tối đa {NotificationRules.MaxCustomers} mã)");
    public static readonly Error ExpiresBeforePublish = Error.Validation("NOTIFICATION_EXPIRES", "Ngày hết hiện phải sau giờ đăng");
    public static readonly Error NotFound = Error.NotFound("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo");
    public static readonly Error EmailNotConfigured = Error.Validation("EMAIL_NOT_CONFIGURED",
        "Máy chủ chưa cấu hình tài khoản gửi email (Email:UserName / Email:Password)");
    public static readonly Error TestEmailInvalid = Error.Validation("NOTIFICATION_TEST_EMAIL",
        $"Nhập email nhận thử hợp lệ (tối đa {NotificationEmail.MaxTestRecipients} địa chỉ, cách nhau bằng dấu phẩy)");

    public static Error TestEmailFailed(string message) => Error.BusinessRule("NOTIFICATION_TEST_EMAIL_FAILED", $"Gửi email thử không được: {message}");

    public static Error Item(int index, string message) => Error.Validation("NOTIFICATION_ITEM", $"Tin {index + 1}: {message}");

    public static Error UnknownCustomers(IEnumerable<string> codes) =>
        Error.Validation("NOTIFICATION_UNKNOWN_CUSTOMERS", $"Không tìm thấy khách: {string.Join(", ", codes)}");
}
