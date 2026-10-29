using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Dựng phản hồi thông báo cho trang quản trị (dùng chung cho danh sách, lưu, gỡ).</summary>
internal static class AdminNotificationMapper
{
    /// <summary>Thông báo → dòng ở trang quản trị: trạng thái theo <paramref name="now"/>, số khách nhận, số khách / tài khoản đã đọc.</summary>
    public static async Task<IReadOnlyList<AdminNotificationDto>> ToDtosAsync(
        IdentityDbContext db, ICustomersApi customers, DateTime now, IReadOnlyList<Notification> rows, CancellationToken ct)
    {
        var ids = rows.Select(n => n.Id).ToList();
        var reads = await db.NotificationReads.AsNoTracking()
            .Where(r => ids.Contains(r.NotificationId))
            .GroupBy(r => r.NotificationId)
            .Select(g => new { Id = g.Key, Accounts = g.Count(), Customers = g.Select(r => r.CustomerId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Id, ct);
        var total = await db.PortalCustomers().CountAsync(ct);
        var typeCounts = await db.TypeCountsAsync(ct);
        var recipientIds = rows.SelectMany(n => n.RecipientIds).Distinct().ToList();
        var codes = recipientIds.Count == 0 ? [] : (await customers.GetByIdsAsync(recipientIds, ct)).ToDictionary(p => p.Id, p => p.Code);

        return rows.Select(n =>
        {
            var types = n.CustomerTypeIds;
            var recipients = n.Audience switch
            {
                NotificationAudience.Types => types.Sum(t => typeCounts.GetValueOrDefault(t)),
                NotificationAudience.Customers => n.RecipientIds.Count,
                _ => total
            };
            var read = reads.GetValueOrDefault(n.Id);
            return new AdminNotificationDto(
                n.Id.ToString(CultureInfo.InvariantCulture), n.Title, NotificationRules.StatusOf(n, now), n.Important, n.Pinned,
                n.PublishAt.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
                n.ExpiresAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                NotificationRules.Time(n.CreatedAt), n.CreatedBy,
                n.Audience, types, n.RecipientIds.Select(id => codes.GetValueOrDefault(id) ?? id.ToString(CultureInfo.InvariantCulture)).ToList(),
                recipients, read?.Customers ?? 0, read?.Accounts ?? 0,
                n.EmailStatus, n.EmailRecipients, n.EmailSent, n.EmailError,
                n.Items.Count > 0 ? NotificationRules.Items(n.Items) : [new NotificationItemDto(null, n.Body, [], n.Link)]);
        }).ToList();
    }
}
