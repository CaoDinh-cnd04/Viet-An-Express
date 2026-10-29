using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal sealed record MarkNotificationReadCommand(long Id) : IRequest<Result<bool>>;

internal sealed class MarkNotificationReadHandler(IdentityDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<MarkNotificationReadCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(MarkNotificationReadCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        if (!await (await db.VisibleNotificationsAsync(customerId, VietnamTime.Now(clock), ct)).AnyAsync(n => n.Id == c.Id, ct)) return NotificationErrors.NotFound;
        var staff = CustomerInbox.StaffKey(user);
        if (await db.NotificationReads.AnyAsync(r => r.NotificationId == c.Id && r.CustomerId == customerId && r.StaffId == staff, ct)) return true;
        db.NotificationReads.Add(new NotificationRead(c.Id, customerId, staff, VietnamTime.Now(clock)));
        await db.SaveReadsIgnoringDuplicateAsync(ct);
        return true;
    }
}
