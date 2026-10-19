using System.Globalization;
using System.Net.Mail;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>Thông báo ở Trung tâm thông báo của khách — tên trường khớp frontend (<c>features/notifications/api.ts</c>).</summary>
internal sealed record NotificationDto(string Id, bool Imp, bool Unread, string Title, string Date, string Body, string? Link);

internal sealed record NotificationListDto(IReadOnlyList<NotificationDto> Data, int UnreadCount);

/// <summary>Thông báo ở trang quản trị: kèm kết quả gửi email và số tài khoản đã đọc.</summary>
internal sealed record AdminNotificationDto(
    string Id, string Title, string Body, string? Link, bool Important, string CreatedAt, string CreatedBy,
    string EmailStatus, int EmailRecipients, int EmailSent, string? EmailError, int ReadCount);

internal static class NotificationErrors
{
    public static readonly Error TitleRequired = Error.Validation("NOTIFICATION_TITLE", $"Nhập tiêu đề (tối đa {Notification.TitleMaxLength} ký tự)");
    public static readonly Error BodyRequired = Error.Validation("NOTIFICATION_BODY", $"Nhập nội dung (tối đa {Notification.BodyMaxLength} ký tự)");
    public static readonly Error Link = Error.Validation("NOTIFICATION_LINK",
        $"Đường link phải bắt đầu bằng http:// hoặc https:// (tối đa {Notification.LinkMaxLength} ký tự)");
    public static readonly Error NotFound = Error.NotFound("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo");
    public static readonly Error EmailNotConfigured = Error.Validation("EMAIL_NOT_CONFIGURED",
        "Máy chủ chưa cấu hình tài khoản gửi email (Email:UserName / Email:Password)");
}

/// <summary>Quy tắc thông báo — hàm thuần, có test.</summary>
internal static class NotificationRules
{
    /// <summary>Gmail cá nhân giới hạn ~500 người nhận / ngày — chừa lại cho email góp ý / hỗ trợ.</summary>
    public const int MaxEmailRecipients = 450;

    /// <summary>Số người nhận (BCC) mỗi email — Gmail giới hạn 100 / email.</summary>
    public const int EmailBatchSize = 50;

    public static Error? Validate(string? title, string? body, string? link = null)
    {
        var t = title?.Trim() ?? "";
        var b = body?.Trim() ?? "";
        if (t.Length is 0 or > Notification.TitleMaxLength) return NotificationErrors.TitleRequired;
        if (b.Length is 0 or > Notification.BodyMaxLength) return NotificationErrors.BodyRequired;
        if (!string.IsNullOrWhiteSpace(link) && !IsWebLink(link)) return NotificationErrors.Link;
        return null;
    }

    /// <summary>Link tuyệt đối http / https (chặn javascript:, data:… vì khách bấm mở trực tiếp).</summary>
    public static bool IsWebLink(string value)
    {
        var v = value.Trim();
        return v.Length <= Notification.LinkMaxLength
            && Uri.TryCreate(v, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>Email hợp lệ, không trùng (không phân biệt hoa thường), tối đa <paramref name="max"/>; trả kèm số địa chỉ bị cắt do vượt giới hạn.</summary>
    public static (IReadOnlyList<string> Emails, int Skipped) Recipients(IEnumerable<string?> emails, int max)
    {
        var valid = emails
            .Select(e => e?.Trim() ?? "")
            .Where(e => e.Length > 0 && MailAddress.TryCreate(e, out var a) && a.Address == e && e.Contains('.', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (valid.Take(max).ToList(), Math.Max(0, valid.Count - max));
    }

    public static string Time(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Email thông báo gửi khách (chưa có người nhận — job gắn BCC theo lô).</summary>
    public static EmailMessage Email(Notification n, EmailCompany company)
    {
        var content = new EmailContent(
            Title: n.Title,
            Badge: n.Important ? "Thông báo quan trọng" : null,
            Meta: $"Việt An Express · {Time(n.CreatedAt)}",
            Sections: [new EmailSection(null, n.Body)],
            Button: n.Link is { } link ? new EmailLink("Xem tại đây", link) : null,
            SecondaryLink: company.Portal("/notifications") is { } portal ? new EmailLink("Xem tất cả thông báo trên portal", portal) : null,
            FooterNote: "Bạn nhận email này vì có tài khoản trên portal Việt An Express. Email gửi tự động, vui lòng không trả lời.");
        return new EmailMessage(
            n.Important ? $"[Quan trọng] {n.Title} — Việt An Express" : $"{n.Title} — Việt An Express",
            EmailTemplate.PlainText(content, company),
            HtmlBody: EmailTemplate.Html(content, company));
    }
}

// ---------------- Khách ----------------

internal sealed record GetNotificationsQuery : IRequest<Result<NotificationListDto>>;

internal sealed record MarkNotificationReadCommand(long Id) : IRequest<Result<bool>>;

internal sealed record MarkAllNotificationsReadCommand : IRequest<Result<int>>;

internal sealed class CustomerNotificationHandlers(IdentityDbContext db, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<GetNotificationsQuery, Result<NotificationListDto>>,
    IRequestHandler<MarkNotificationReadCommand, Result<bool>>,
    IRequestHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    /// <summary>Số thông báo mới nhất khách thấy.</summary>
    public const int ListLimit = 50;

    private long StaffKey => user.StaffId ?? 0;

    public async Task<Result<NotificationListDto>> Handle(GetNotificationsQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var staff = StaffKey;
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == null || n.CustomerId == customerId)
            .OrderByDescending(n => n.Id)
            .Take(ListLimit)
            .Select(n => new
            {
                n.Id, n.Important, n.Title, n.Body, n.Link, n.CreatedAt,
                Read = db.NotificationReads.Any(r => r.NotificationId == n.Id && r.CustomerId == customerId && r.StaffId == staff)
            })
            .ToListAsync(ct);
        var items = rows.Select(r => new NotificationDto(
            r.Id.ToString(CultureInfo.InvariantCulture), r.Important, !r.Read, r.Title, NotificationRules.Time(r.CreatedAt), r.Body, r.Link)).ToList();
        return new NotificationListDto(items, items.Count(i => i.Unread));
    }

    public async Task<Result<bool>> Handle(MarkNotificationReadCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        if (!await db.Notifications.AnyAsync(n => n.Id == c.Id && (n.CustomerId == null || n.CustomerId == customerId), ct)) return NotificationErrors.NotFound;
        var staff = StaffKey;
        if (await db.NotificationReads.AnyAsync(r => r.NotificationId == c.Id && r.CustomerId == customerId && r.StaffId == staff, ct)) return true;
        db.NotificationReads.Add(new NotificationRead(c.Id, customerId, staff, VietnamTime.Now(clock)));
        await SaveIgnoringDuplicateAsync(ct);
        return true;
    }

    public async Task<Result<int>> Handle(MarkAllNotificationsReadCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var staff = StaffKey;
        var unread = await db.Notifications
            .Where(n => n.CustomerId == null || n.CustomerId == customerId)
            .OrderByDescending(n => n.Id)
            .Take(ListLimit)
            .Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.CustomerId == customerId && r.StaffId == staff))
            .Select(n => n.Id)
            .ToListAsync(ct);
        var now = VietnamTime.Now(clock);
        db.NotificationReads.AddRange(unread.Select(id => new NotificationRead(id, customerId, staff, now)));
        await SaveIgnoringDuplicateAsync(ct);
        return unread.Count;
    }

    /// <summary>2 tab bấm đọc cùng lúc → trùng khóa duy nhất: coi như đã đọc.</summary>
    private async Task SaveIgnoringDuplicateAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
    }
}

// ---------------- Quản trị Việt An ----------------

internal sealed record CreateNotificationCommand(string? Title, string? Body, bool Important, bool SendEmail, string? Link = null)
    : IRequest<Result<AdminNotificationDto>>;

internal sealed record GetAdminNotificationsQuery : IRequest<IReadOnlyList<AdminNotificationDto>>;

internal sealed record DeleteNotificationCommand(long Id) : IRequest<Result<bool>>;

internal sealed class AdminNotificationHandlers(
    IdentityDbContext db, ICurrentUser user, IEmailSender email, IBackgroundJobQueue jobs, TimeProvider clock) :
    IRequestHandler<CreateNotificationCommand, Result<AdminNotificationDto>>,
    IRequestHandler<GetAdminNotificationsQuery, IReadOnlyList<AdminNotificationDto>>,
    IRequestHandler<DeleteNotificationCommand, Result<bool>>
{
    private const int AdminListLimit = 200;

    public async Task<Result<AdminNotificationDto>> Handle(CreateNotificationCommand c, CancellationToken ct)
    {
        if (NotificationRules.Validate(c.Title, c.Body, c.Link) is { } error) return error;
        if (c.SendEmail && !email.IsConfigured) return NotificationErrors.EmailNotConfigured;

        var notification = Notification.Create(c.Title!, c.Body!, c.Important, c.SendEmail, user.UserName ?? "", VietnamTime.Now(clock), c.Link);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        if (c.SendEmail)
        {
            var id = notification.Id;
            jobs.Enqueue($"email thông báo #{id}", (services, token) => NotificationEmailJob.RunAsync(services, id, token));
        }
        return ToDto(notification, 0);
    }

    public async Task<IReadOnlyList<AdminNotificationDto>> Handle(GetAdminNotificationsQuery q, CancellationToken ct)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == null)
            .OrderByDescending(n => n.Id)
            .Take(AdminListLimit)
            .Select(n => new { Notification = n, Reads = db.NotificationReads.Count(r => r.NotificationId == n.Id) })
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r.Notification, r.Reads)).ToList();
    }

    public async Task<Result<bool>> Handle(DeleteNotificationCommand c, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == c.Id && n.CustomerId == null, ct);
        if (notification is null) return NotificationErrors.NotFound;
        // Xóa lượt đã đọc trước: không phụ thuộc cascade của khóa ngoại trong database.
        db.NotificationReads.RemoveRange(await db.NotificationReads.Where(r => r.NotificationId == c.Id).ToListAsync(ct));
        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static AdminNotificationDto ToDto(Notification n, int reads) => new(
        n.Id.ToString(CultureInfo.InvariantCulture), n.Title, n.Body, n.Link, n.Important, NotificationRules.Time(n.CreatedAt), n.CreatedBy,
        n.EmailStatus, n.EmailRecipients, n.EmailSent, n.EmailError, reads);
}

/// <summary>
/// Gửi email thông báo ở nền: người nhận = email hồ sơ của khách có tài khoản portal + email tài khoản con đang hoạt động,
/// tối đa <see cref="NotificationRules.MaxEmailRecipients"/> địa chỉ, gửi BCC theo lô. Ghi kết quả vào dbo.ThongBao.
/// </summary>
internal static class NotificationEmailJob
{
    public static async Task RunAsync(IServiceProvider services, long notificationId, CancellationToken ct)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var customers = services.GetRequiredService<ICustomersApi>();
        var email = services.GetRequiredService<IEmailSender>();
        var company = EmailCompany.From(services.GetRequiredService<IConfiguration>());
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(NotificationEmailJob));

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, ct);
        if (notification is null) return;

        var customerIds = await db.Logins.AsNoTracking().Where(l => l.Password != null && l.Password != "").Select(l => l.CustomerId).ToListAsync(ct);
        var staffEmails = await db.StaffAccounts.AsNoTracking().Where(s => s.IsActive && s.Email != null).Select(s => s.Email).ToListAsync(ct);
        var profiles = await customers.GetByIdsAsync(customerIds, ct);
        var (recipients, skipped) = NotificationRules.Recipients(profiles.Select(p => p.Email).Concat(staffEmails), NotificationRules.MaxEmailRecipients);

        var sent = 0;
        string? error = skipped > 0 ? $"Vượt giới hạn {NotificationRules.MaxEmailRecipients} người nhận / lần — {skipped} địa chỉ chưa gửi" : null;
        var template = NotificationRules.Email(notification, company);
        foreach (var batch in recipients.Chunk(NotificationRules.EmailBatchSize))
        {
            try
            {
                if (await email.SendAsync(template with { Bcc = batch }, ct)) sent += batch.Length;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Lỗi gửi email thông báo #{Id} (lô {Count} người nhận)", notificationId, batch.Length);
                error = e.Message;
            }
        }

        notification.RecordEmail(recipients.Count, sent, error);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Email thông báo #{Id}: đã gửi {Sent}/{Total}", notificationId, sent, recipients.Count);
    }
}
