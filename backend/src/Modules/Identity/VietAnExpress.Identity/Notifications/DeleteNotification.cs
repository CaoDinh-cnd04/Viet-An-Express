using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

internal sealed record DeleteNotificationCommand(long Id) : IRequest<Result<bool>>;

internal sealed class DeleteNotificationHandler(IdentityDbContext db) : IRequestHandler<DeleteNotificationCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteNotificationCommand c, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == c.Id && n.CustomerId == null, ct);
        if (notification is null) return NotificationErrors.NotFound;
        // Xóa lượt đọc trước: không phụ thuộc cascade của khóa ngoại trong database.
        db.NotificationReads.RemoveRange(await db.NotificationReads.Where(r => r.NotificationId == c.Id).ToListAsync(ct));
        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
