using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Truy vấn dùng chung: thông báo khách thấy và khách nhận thông báo.</summary>
internal static class NotificationQueries
{
    /// <summary>
    /// Thông báo riêng của khách + thông báo đã đăng, đang trong thời gian hiện, gửi tới khách
    /// (mọi khách / đúng loại khách / có trong danh sách chỉ định).
    /// </summary>
    public static IQueryable<Notification> VisibleTo(this IQueryable<Notification> q, IdentityDbContext db, long customerId, int? customerType, DateTime now)
    {
        var typeToken = customerType is { } type ? $",{type}," : null;
        var customerToken = Notification.Token(customerId);
        return q.Where(n => n.CustomerId == customerId
            || (n.CustomerId == null && n.Status == NotificationStatus.Published && n.PublishAt <= now && (n.ExpiresAt == null || n.ExpiresAt >= now)
                && (n.Audience == NotificationAudience.All
                    || (n.Audience == NotificationAudience.Types && typeToken != null && n.CustomerTypes != null && n.CustomerTypes.Contains(typeToken))
                    || (n.Audience == NotificationAudience.Customers && n.Recipients != null && n.Recipients.Contains(customerToken)))));
    }

    /// <summary>Khách có tài khoản portal (đã đặt mật khẩu).</summary>
    public static IQueryable<CustomerLogin> PortalCustomers(this IdentityDbContext db) =>
        db.Logins.Where(l => l.Password != null && l.Password != "");

    /// <summary>Khách nhận 1 thông báo quản trị đăng.</summary>
    public static IQueryable<long> AudienceOf(this IdentityDbContext db, Notification n)
    {
        var types = n.CustomerTypeIds.ToList();
        var ids = n.RecipientIds.ToList();
        return n.Audience switch
        {
            NotificationAudience.Types => db.PortalCustomers().Where(l => l.CustomerTypeId != null && types.Contains(l.CustomerTypeId.Value)).Select(l => l.CustomerId),
            NotificationAudience.Customers => db.Logins.Where(l => ids.Contains(l.CustomerId)).Select(l => l.CustomerId),
            _ => db.PortalCustomers().Select(l => l.CustomerId)
        };
    }

    /// <summary>Số khách có tài khoản portal theo loại (null = chưa phân loại).</summary>
    public static async Task<Dictionary<int, int>> TypeCountsAsync(this IdentityDbContext db, CancellationToken ct) =>
        (await db.PortalCustomers().Where(l => l.CustomerTypeId != null)
            .GroupBy(l => l.CustomerTypeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct))
        .ToDictionary(x => x.Key, x => x.Count);
}
