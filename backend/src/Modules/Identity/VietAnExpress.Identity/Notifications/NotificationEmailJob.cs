using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.Notifications;

/// <summary>
/// Gửi email thông báo ở nền: người nhận = email hồ sơ của khách thuộc đối tượng nhận (có tài khoản portal) + email tài khoản con đang hoạt động
/// của các khách đó, tối đa <see cref="NotificationEmail.MaxEmailRecipients"/> địa chỉ, gửi BCC theo lô. Ghi kết quả vào dbo.TThongBao.
/// </summary>
internal static class NotificationEmailJob
{
    public static void Enqueue(IBackgroundJobQueue jobs, long notificationId) =>
        jobs.Enqueue($"email thông báo #{notificationId}", (services, token) => RunAsync(services, notificationId, token));

    public static async Task RunAsync(IServiceProvider services, long notificationId, CancellationToken ct)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var customers = services.GetRequiredService<ICustomersApi>();
        var email = services.GetRequiredService<IEmailSender>();
        var company = EmailCompany.From(services.GetRequiredService<IConfiguration>());
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(NotificationEmailJob));

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, ct);
        if (notification is null || notification.Status != NotificationStatus.Published) return;

        var customerIds = await db.AudienceOf(notification).Distinct().ToListAsync(ct);
        var staffEmails = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.IsActive && s.Email != null && customerIds.Contains(s.CustomerId))
            .Select(s => s.Email)
            .ToListAsync(ct);
        var profiles = await customers.GetByIdsAsync(customerIds, ct);
        var (recipients, skipped) = NotificationEmail.Recipients(profiles.Select(p => p.Email).Concat(staffEmails), NotificationEmail.MaxEmailRecipients);

        var sent = 0;
        string? error = skipped > 0 ? $"Vượt giới hạn {NotificationEmail.MaxEmailRecipients} người nhận / lần — {skipped} địa chỉ chưa gửi" : null;
        var template = NotificationEmail.Email(notification, company, NotificationEmail.InlineImages(notification, services.GetService<NotificationImageStore>()));
        foreach (var batch in recipients.Chunk(NotificationEmail.EmailBatchSize))
        {
            try
            {
                if (await email.SendAsync(template with { Bcc = batch }, ct)) sent += batch.Length;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Lỗi gửi email thông báo #{Id} (lô {Count} người nhận)", notificationId, batch.Length);
                error = e.Message;
            }
        }

        notification.RecordEmail(recipients.Count, sent, error);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Email thông báo #{Id}: đã gửi {Sent}/{Total}", notificationId, sent, recipients.Count);
    }

    /// <summary>Thông báo hẹn giờ có email đã tới giờ đăng → chuyển sang đang gửi và xếp việc gửi. Trả số thông báo đã xếp.</summary>
    public static async Task<int> StartDueAsync(IdentityDbContext db, IBackgroundJobQueue jobs, TimeProvider clock, CancellationToken ct)
    {
        var now = VietnamTime.Now(clock);
        var due = await db.Notifications
            .Where(n => n.Status == NotificationStatus.Published && n.EmailStatus == NotificationEmailStatus.Scheduled && n.PublishAt <= now)
            .ToListAsync(ct);
        var started = due.Where(n => n.StartScheduledEmail(now)).ToList();
        if (started.Count == 0) return 0;
        await db.SaveChangesAsync(ct);
        foreach (var n in started) Enqueue(jobs, n.Id);
        return started.Count;
    }
}
