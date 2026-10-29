
namespace VietAnExpress.Identity.Notifications;

/// <summary>1 tài khoản đã đọc 1 thông báo (dbo.TThongBao_DaDoc). <see cref="StaffId"/> = 0: tài khoản chính của khách.</summary>
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
