using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal sealed record GetNotificationAudienceQuery : IRequest<NotificationAudienceDto>;

internal sealed class GetNotificationAudienceHandler(IdentityDbContext db)
    : IRequestHandler<GetNotificationAudienceQuery, NotificationAudienceDto>
{
    public async Task<NotificationAudienceDto> Handle(GetNotificationAudienceQuery q, CancellationToken ct)
    {
        var total = await db.PortalCustomers().CountAsync(ct);
        var counts = await db.TypeCountsAsync(ct);
        var names = await db.CustomerTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        // Mọi loại trong danh mục (kể cả chưa có khách) + mã loại lạ khách đang dùng mà danh mục không có.
        var types = names.Keys.Union(counts.Keys)
            .Order()
            .Select(id => new CustomerTypeDto(id,
                names.GetValueOrDefault(id) is { Length: > 0 } name ? name.Trim() : $"Loại {id}",
                counts.GetValueOrDefault(id)))
            .ToList();
        return new NotificationAudienceDto(total, types);
    }
}
