namespace VietAnExpress.Identity.Domain;

/// <summary>Trạng thái gửi email của 1 thông báo.</summary>
internal static class NotificationEmailStatus
{
    /// <summary>Không chọn gửi email.</summary>
    public const string None = "none";
    /// <summary>Đã xếp hàng, đang gửi ở nền.</summary>
    public const string Sending = "sending";
    public const string Sent = "sent";
    /// <summary>Gửi được một phần (có lô lỗi hoặc vượt giới hạn người nhận).</summary>
    public const string Partial = "partial";
    public const string Failed = "failed";
    /// <summary>Không có khách nào có email hợp lệ.</summary>
    public const string NoRecipients = "no_recipients";

    public const int MaxLength = 20;
}

/// <summary>Thông báo quản trị Việt An gửi mọi khách (dbo.ThongBao) — hiện ở Trung tâm thông báo, có thể gửi kèm email.</summary>
internal sealed class Notification
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 4000;
    public const int ErrorMaxLength = 500;
    public const int LinkMaxLength = 500;

    private Notification() { } // EF Core

    public static Notification Create(string title, string body, bool important, bool sendEmail, string createdBy, DateTime now, string? link = null,
        long? customerId = null) => new()
    {
        CustomerId = customerId,
        Title = title.Trim(),
        Body = body.Trim(),
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
        Important = important,
        CreatedBy = createdBy,
        CreatedAt = now,
        EmailStatus = sendEmail ? NotificationEmailStatus.Sending : NotificationEmailStatus.None
    };

    public long Id { get; private set; }
    /// <summary>Gửi riêng 1 khách (vd phản hồi sự cố); null = mọi khách.</summary>
    public long? CustomerId { get; private set; }
    public string Title { get; private set; } = "";
    public string Body { get; private set; } = "";
    /// <summary>Đường link "Xem tại đây" (http / https, không bắt buộc).</summary>
    public string? Link { get; private set; }
    /// <summary>Quan trọng → hiện popup khi khách mở portal.</summary>
    public bool Important { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = "";
    public string EmailStatus { get; private set; } = NotificationEmailStatus.None;
    /// <summary>Số địa chỉ email định gửi.</summary>
    public int EmailRecipients { get; private set; }
    /// <summary>Số địa chỉ đã gửi thành công.</summary>
    public int EmailSent { get; private set; }
    public string? EmailError { get; private set; }

    /// <summary>Ghi kết quả gửi email ở nền.</summary>
    public void RecordEmail(int recipients, int sent, string? error)
    {
        EmailRecipients = recipients;
        EmailSent = sent;
        EmailError = error is null ? null : error.Length <= ErrorMaxLength ? error : error[..ErrorMaxLength];
        EmailStatus = recipients == 0 ? NotificationEmailStatus.NoRecipients
            : sent == 0 ? NotificationEmailStatus.Failed
            : sent < recipients || error is not null ? NotificationEmailStatus.Partial
            : NotificationEmailStatus.Sent;
    }
}

/// <summary>1 tài khoản đã đọc 1 thông báo (dbo.ThongBao_DaDoc). <see cref="StaffId"/> = 0: tài khoản chính của khách.</summary>
internal sealed class NotificationRead
{
    private NotificationRead() { } // EF Core

    public NotificationRead(long notificationId, long customerId, long staffId, DateTime now)
    {
        NotificationId = notificationId;
        CustomerId = customerId;
        StaffId = staffId;
        ReadAt = now;
    }

    public long Id { get; private set; }
    public long NotificationId { get; private set; }
    public long CustomerId { get; private set; }
    public long StaffId { get; private set; }
    public DateTime ReadAt { get; private set; }
}
