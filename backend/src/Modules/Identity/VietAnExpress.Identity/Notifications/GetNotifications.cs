using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal sealed record GetNotificationsQuery : IRequest<Result<NotificationListDto>>;

internal sealed class GetNotificationsHandler(IdentityDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<GetNotificationsQuery, Result<NotificationListDto>>
{
    public async Task<Result<NotificationListDto>> Handle(GetNotificationsQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var staff = CustomerInbox.StaffKey(user);
        var rows = await (await db.VisibleNotificationsAsync(customerId, VietnamTime.Now(clock), ct)).AsNoTracking()
            .OrderByDescending(n => n.Pinned && n.CustomerId == null)
            .ThenByDescending(n => n.PublishAt)
            .ThenByDescending(n => n.Id)
            .Take(CustomerInbox.ListLimit)
            .Select(n => new
            {
                Notification = n,
                Read = db.NotificationReads.Any(r => r.NotificationId == n.Id && r.CustomerId == customerId && r.StaffId == staff)
            })
            .ToListAsync(ct);
        var items = rows.Select(r => new NotificationDto(
            r.Notification.Id.ToString(CultureInfo.InvariantCulture), r.Notification.Important, r.Notification.Pinned, !r.Read,
            r.Notification.CustomerId is null ? "notice" : "support",
            r.Notification.Title, NotificationRules.Time(r.Notification.PublishAt), r.Notification.Body, r.Notification.Link,
            NotificationRules.Items(r.Notification.Items))).ToList();
        return new NotificationListDto(items, items.Count(i => i.Unread));
    }
}
