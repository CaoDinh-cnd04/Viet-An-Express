using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Notifications;

/// <summary>1 tin trong thông báo — <c>images</c> là tên tệp ảnh (đọc ở GET /public/notifications/images/{tên}).</summary>
/// <c>body</c> / <c>detail</c>: chữ thường hoặc HTML có định dạng (đã lọc); <c>detail</c> có → nút "Xem chi tiết" mở nội dung này thay vì <c>link</c>.
internal sealed record NotificationItemDto(string? Title, string Body, IReadOnlyList<string> Images, string? Link, string? Detail = null);

/// <summary>
/// Thông báo ở Trung tâm thông báo của khách — tên trường khớp frontend (<c>features/notifications/api.ts</c>).
/// <c>kind</c>: "notice" (Việt An đăng) / "support" (cập nhật hỗ trợ &amp; sự cố gửi riêng khách). <c>items</c> trống = thông báo cũ chỉ có body + link.
/// </summary>
internal sealed record NotificationDto(
    string Id, bool Imp, bool Pin, bool Unread, string Kind, string Title, string Date, string Body, string? Link, IReadOnlyList<NotificationItemDto> Items);

internal sealed record NotificationListDto(IReadOnlyList<NotificationDto> Data, int UnreadCount);

/// <summary>Thông báo ở trang quản trị. <c>status</c>: draft / scheduled / live / expired / removed.</summary>
internal sealed record AdminNotificationDto(
    string Id, string Title, string Status, bool Important, bool Pinned,
    string PublishAt, string? ExpiresOn, string CreatedAt, string CreatedBy,
    string Audience, IReadOnlyList<int> CustomerTypes, IReadOnlyList<string> CustomerCodes,
    int Recipients, int ReadCustomers, int ReadCount,
    string EmailStatus, int EmailRecipients, int EmailSent, string? EmailError,
    IReadOnlyList<NotificationItemDto> Items);

internal sealed record CustomerTypeDto(int Id, string Name, int Count);

/// <summary>Số khách có tài khoản portal, chia theo loại khách (TCustomer.CustomerTypeID, tên ở dbo.TCustomerType).</summary>
internal sealed record NotificationAudienceDto(int Total, IReadOnlyList<CustomerTypeDto> Types);

/// <summary>Khách đã đọc 1 thông báo: số tài khoản (chính / con) đã đọc và lần đọc đầu.</summary>
internal sealed record NotificationReaderDto(string CustomerCode, string CompanyName, int Accounts, string ReadAt);

internal sealed record NotificationItemInput(string? Title, string? Body, IReadOnlyList<string>? Images, string? Link, string? Detail = null);
