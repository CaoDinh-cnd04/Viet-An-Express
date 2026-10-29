using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.Notifications;

internal sealed record GetAdminNotificationsQuery : IRequest<IReadOnlyList<AdminNotificationDto>>;

internal sealed class GetAdminNotificationsHandler(IdentityDbContext db, ICustomersApi customers, TimeProvider clock)
    : IRequestHandler<GetAdminNotificationsQuery, IReadOnlyList<AdminNotificationDto>>
{
    private const int AdminListLimit = 200;

    public async Task<IReadOnlyList<AdminNotificationDto>> Handle(GetAdminNotificationsQuery q, CancellationToken ct)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == null)
            .OrderByDescending(n => n.Id)
            .Take(AdminListLimit)
            .ToListAsync(ct);
        return await AdminNotificationMapper.ToDtosAsync(db, customers, VietnamTime.Now(clock), rows, ct);
    }
}
