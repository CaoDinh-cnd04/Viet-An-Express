using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Trung tâm thông báo của khách: thông báo khách thấy và lượt đã đọc (riêng từng tài khoản chính / con).</summary>
internal static class CustomerInbox
{
    /// <summary>Số thông báo mới nhất khách thấy.</summary>
    public const int ListLimit = 50;

    /// <summary>Khóa tài khoản trong dbo.TThongBao_DaDoc: 0 = tài khoản chính, còn lại = ID tài khoản con.</summary>
    public static long StaffKey(ICurrentUser user) => user.StaffId ?? 0;

    /// <summary>Thông báo khách thấy lúc <paramref name="now"/> (theo loại khách trong dbo.TCustomer).</summary>
    public static async Task<IQueryable<Notification>> VisibleNotificationsAsync(this IdentityDbContext db, long customerId, DateTime now, CancellationToken ct)
    {
        var type = await db.Logins.AsNoTracking().Where(l => l.CustomerId == customerId).Select(l => l.CustomerTypeId).FirstOrDefaultAsync(ct);
        return db.Notifications.VisibleTo(db, customerId, type, now);
    }

    /// <summary>2 tab bấm đọc cùng lúc → trùng khóa duy nhất: coi như đã đọc.</summary>
    public static async Task SaveReadsIgnoringDuplicateAsync(this IdentityDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
    }
}
