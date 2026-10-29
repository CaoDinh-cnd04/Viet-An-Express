using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal sealed record MarkAllNotificationsReadCommand : IRequest<Result<int>>;

internal sealed class MarkAllNotificationsReadHandler(IdentityDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MarkAllNotificationsReadCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var staff = CustomerInbox.StaffKey(user);
        var unread = await (await db.VisibleNotificationsAsync(customerId, VietnamTime.Now(clock), ct))
            .OrderByDescending(n => n.Pinned && n.CustomerId == null)
            .ThenByDescending(n => n.PublishAt)
            .ThenByDescending(n => n.Id)
            .Take(CustomerInbox.ListLimit)
            .Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.CustomerId == customerId && r.StaffId == staff))
            .Select(n => n.Id)
            .ToListAsync(ct);
        var now = VietnamTime.Now(clock);
        db.NotificationReads.AddRange(unread.Select(id => new NotificationRead(id, customerId, staff, now)));
        await db.SaveReadsIgnoringDuplicateAsync(ct);
        return unread.Count;
    }
}
