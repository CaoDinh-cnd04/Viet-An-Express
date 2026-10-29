using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Gỡ thông báo khỏi portal của khách (giữ lại ở quản trị, đăng lại được).</summary>
internal sealed record RemoveNotificationCommand(long Id) : IRequest<Result<AdminNotificationDto>>;

internal sealed class RemoveNotificationHandler(IdentityDbContext db, ICustomersApi customers, TimeProvider clock)
    : IRequestHandler<RemoveNotificationCommand, Result<AdminNotificationDto>>
{
    public async Task<Result<AdminNotificationDto>> Handle(RemoveNotificationCommand c, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == c.Id && n.CustomerId == null, ct);
        if (notification is null) return NotificationErrors.NotFound;
        notification.Remove();
        await db.SaveChangesAsync(ct);
        return (await AdminNotificationMapper.ToDtosAsync(db, customers, VietnamTime.Now(clock), [notification], ct))[0];
    }
}
