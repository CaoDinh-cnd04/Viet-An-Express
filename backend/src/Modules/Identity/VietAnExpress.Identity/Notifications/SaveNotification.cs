using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

/// <summary>
/// Tạo (<see cref="Id"/> null) hoặc sửa thông báo. <see cref="Publish"/> false = lưu nháp; true = đăng vào <see cref="PublishAt"/>
/// (trống / đã qua = ngay). <see cref="CustomerTypes"/> dùng khi <see cref="Audience"/> = type, <see cref="CustomerCodes"/> khi = pick.
/// </summary>
internal sealed record SaveNotificationCommand(
    long? Id, string? Title, IReadOnlyList<NotificationItemInput>? Items, bool Important, bool Pinned, bool SendEmail,
    string? Audience, IReadOnlyList<int>? CustomerTypes, IReadOnlyList<string>? CustomerCodes,
    DateTime? PublishAt, DateOnly? ExpiresOn, bool Publish) : IRequest<Result<AdminNotificationDto>>;

internal sealed class SaveNotificationHandler(IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IEmailSender email, IBackgroundJobQueue jobs, NotificationImageStore images, TimeProvider clock)
    : IRequestHandler<SaveNotificationCommand, Result<AdminNotificationDto>>
{
    public async Task<Result<AdminNotificationDto>> Handle(SaveNotificationCommand c, CancellationToken ct)
    {
        var content = NotificationRules.ValidateContent(c.Title, c.Items);
        if (content.IsFailure) return content.Error;
        var audience = c.Audience ?? NotificationAudience.All;
        if (!NotificationAudience.IsValid(audience)) return NotificationErrors.Audience;
        var types = (c.CustomerTypes ?? []).Distinct().ToList();
        if (audience == NotificationAudience.Types && types.Count == 0) return NotificationErrors.TypesRequired;
        var codes = NotificationRules.CustomerCodes(c.CustomerCodes);
        if (audience == NotificationAudience.Customers && codes.Count is 0 or > NotificationRules.MaxCustomers) return NotificationErrors.CustomersRequired;
        if (c.Publish && c.SendEmail && !email.IsConfigured) return NotificationErrors.EmailNotConfigured;

        if (NotificationItemsBuilder.Build(content.Value, images) is not { } items) return NotificationErrors.ImageNotFound;

        var customerIds = new List<long>();
        if (audience == NotificationAudience.Customers)
        {
            var unknown = new List<string>();
            foreach (var code in codes)
            {
                if (await customers.GetByCodeAsync(code, ct) is { } found) customerIds.Add(found.Id);
                else unknown.Add(code);
            }
            if (unknown.Count > 0) return NotificationErrors.UnknownCustomers(unknown);
        }

        var now = VietnamTime.Now(clock);
        var expiresAt = NotificationRules.EndOfDay(c.ExpiresOn);
        if (c.Publish && expiresAt is { } end && end < (c.PublishAt is { } at && at > now ? at : now)) return NotificationErrors.ExpiresBeforePublish;

        Notification notification;
        if (c.Id is { } id)
        {
            var existing = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.CustomerId == null, ct);
            if (existing is null) return NotificationErrors.NotFound;
            notification = existing;
        }
        else
        {
            notification = Notification.Draft(user.UserName ?? "", now);
            db.Notifications.Add(notification);
        }

        notification.SetContent(c.Title!, items, c.Important, c.Pinned, expiresAt, audience, types, customerIds);
        var sendNow = false;
        if (c.Publish) sendNow = notification.Publish(c.PublishAt, c.SendEmail, now);
        else notification.SaveAsDraft(c.SendEmail);
        await db.SaveChangesAsync(ct);

        if (sendNow) NotificationEmailJob.Enqueue(jobs, notification.Id);
        return (await AdminNotificationMapper.ToDtosAsync(db, customers, VietnamTime.Now(clock), [notification], ct))[0];
    }
}
