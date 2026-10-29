using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal sealed record GetNotificationReadersQuery(long Id) : IRequest<Result<IReadOnlyList<NotificationReaderDto>>>;

internal sealed class GetNotificationReadersHandler(IdentityDbContext db, ICustomersApi customers)
    : IRequestHandler<GetNotificationReadersQuery, Result<IReadOnlyList<NotificationReaderDto>>>
{
    private const int ReaderLimit = 500;

    public async Task<Result<IReadOnlyList<NotificationReaderDto>>> Handle(GetNotificationReadersQuery q, CancellationToken ct)
    {
        if (!await db.Notifications.AnyAsync(n => n.Id == q.Id && n.CustomerId == null, ct)) return NotificationErrors.NotFound;
        var reads = await db.NotificationReads.AsNoTracking()
            .Where(r => r.NotificationId == q.Id)
            .GroupBy(r => r.CustomerId)
            .Select(g => new { CustomerId = g.Key, Accounts = g.Count(), ReadAt = g.Min(r => r.ReadAt) })
            .OrderByDescending(r => r.ReadAt)
            .Take(ReaderLimit)
            .ToListAsync(ct);
        var profiles = (await customers.GetByIdsAsync(reads.Select(r => r.CustomerId).ToList(), ct)).ToDictionary(p => p.Id);
        return reads.Select(r => profiles.TryGetValue(r.CustomerId, out var p)
                ? new NotificationReaderDto(p.Code, p.CompanyName, r.Accounts, NotificationRules.Time(r.ReadAt))
                : new NotificationReaderDto(r.CustomerId.ToString(CultureInfo.InvariantCulture), "", r.Accounts, NotificationRules.Time(r.ReadAt)))
            .ToList();
    }
}
