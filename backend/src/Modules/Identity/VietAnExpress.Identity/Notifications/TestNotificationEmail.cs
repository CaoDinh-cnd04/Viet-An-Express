using MediatR;
using Microsoft.Extensions.Configuration;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Gửi thử email của nội dung đang soạn (chưa cần lưu) tới <see cref="To"/> — không ghi gì vào DB. Trả số địa chỉ đã gửi.</summary>
internal sealed record TestNotificationEmailCommand(string? To, string? Title, IReadOnlyList<NotificationItemInput>? Items, bool Important)
    : IRequest<Result<int>>;

internal sealed class TestNotificationEmailHandler(ICurrentUser user, IEmailSender email, NotificationImageStore images, TimeProvider clock, IConfiguration config)
    : IRequestHandler<TestNotificationEmailCommand, Result<int>>
{
    public async Task<Result<int>> Handle(TestNotificationEmailCommand c, CancellationToken ct)
    {
        if (NotificationEmail.TestRecipients(c.To) is not { } to) return NotificationErrors.TestEmailInvalid;
        var content = NotificationRules.ValidateContent(c.Title, c.Items);
        if (content.IsFailure) return content.Error;
        if (!email.IsConfigured) return NotificationErrors.EmailNotConfigured;

        if (NotificationItemsBuilder.Build(content.Value, images) is not { } items) return NotificationErrors.ImageNotFound;

        // Bản tạm trong bộ nhớ (không thêm vào DbContext) — dùng chung mẫu email với lần gửi thật.
        var draft = Notification.Draft(user.UserName ?? "", VietnamTime.Now(clock));
        draft.SetContent(c.Title!, items, c.Important, false, null, NotificationAudience.All, [], []);
        var message = NotificationEmail.Email(draft, EmailCompany.From(config), NotificationEmail.InlineImages(draft, images));
        try
        {
            if (!await email.SendAsync(message with { Subject = "[Gửi thử] " + message.Subject, To = to }, ct)) return NotificationErrors.EmailNotConfigured;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return NotificationErrors.TestEmailFailed(e.Message);
        }
        return to.Count;
    }
}
