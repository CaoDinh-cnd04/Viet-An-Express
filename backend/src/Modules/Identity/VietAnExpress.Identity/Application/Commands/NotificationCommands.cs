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

/// <summary>1 tin trong thông báo — <c>images</c> là tên tệp ảnh (đọc ở GET /public/notifications/images/{tên}).</summary>
/// <c>body</c> / <c>detail</c>: chữ thường hoặc HTML có định dạng (đã lọc); <c>detail</c> có → nút "Xem chi tiết" mở nội dung này thay vì <c>link</c>.
internal sealed record NotificationItemDto(string? Title, string Body, IReadOnlyList<string> Images, string? Link, string? Detail = null);

/// <summary>
/// Thông báo ở Trung tâm thông báo của khách — tên trường khớp frontend (<c>features/notifications/api.ts</c>).
/// <c>kind</c>: "notice" (Việt An đăng) / "support" (cập nhật hỗ trợ &amp; sự cố gửi riêng khách). <c>items</c> trống = thông báo cũ chỉ có body + link.
/// </summary>
internal sealed record NotificationDto(
    string Id, bool Imp, bool Pin, bool Unread, string Kind, string Title, string Date, string Body, string? Link, IReadOnlyList<NotificationItemDto> Items);

internal sealed record NotificationListDto(IReadOnlyList<NotificationDto> Data, int UnreadCount);

/// <summary>Thông báo ở trang quản trị. <c>status</c>: draft / scheduled / live / expired / removed.</summary>
internal sealed record AdminNotificationDto(
    string Id, string Title, string Status, bool Important, bool Pinned,
    string PublishAt, string? ExpiresOn, string CreatedAt, string CreatedBy,
    string Audience, IReadOnlyList<int> CustomerTypes, IReadOnlyList<string> CustomerCodes,
    int Recipients, int ReadCustomers, int ReadCount,
    string EmailStatus, int EmailRecipients, int EmailSent, string? EmailError,
    IReadOnlyList<NotificationItemDto> Items);

internal sealed record CustomerTypeDto(int Id, string Name, int Count);

/// <summary>Số khách có tài khoản portal, chia theo loại khách (TCustomer.CustomerTypeID, tên ở dbo.TCustomerType).</summary>
internal sealed record NotificationAudienceDto(int Total, IReadOnlyList<CustomerTypeDto> Types);

/// <summary>Khách đã đọc 1 thông báo: số tài khoản (chính / con) đã đọc và lần đọc đầu.</summary>
internal sealed record NotificationReaderDto(string CustomerCode, string CompanyName, int Accounts, string ReadAt);

internal sealed record NotificationItemInput(string? Title, string? Body, IReadOnlyList<string>? Images, string? Link, string? Detail = null);


internal static class NotificationErrors
{
    public static readonly Error TitleRequired = Error.Validation("NOTIFICATION_TITLE", $"Nhập tiêu đề (tối đa {Notification.TitleMaxLength} ký tự)");
    public static readonly Error BodyRequired = Error.Validation("NOTIFICATION_BODY", $"Nhập nội dung (tối đa {Notification.BodyMaxLength} ký tự)");
    public static readonly Error Link = Error.Validation("NOTIFICATION_LINK",
        $"Đường link phải bắt đầu bằng http:// hoặc https:// (tối đa {Notification.LinkMaxLength} ký tự)");
    public static readonly Error ItemsRequired = Error.Validation("NOTIFICATION_ITEMS", $"Thông báo cần 1 – {NotificationRules.MaxItems} tin");
    public static readonly Error ImageNotFound = Error.Validation("NOTIFICATION_IMAGE", "Có ảnh không còn trên máy chủ — bỏ ảnh đó và tải lên lại");
    public static readonly Error Audience = Error.Validation("NOTIFICATION_AUDIENCE", "Chọn đối tượng nhận: mọi khách, theo loại khách hoặc chọn khách");
    public static readonly Error TypesRequired = Error.Validation("NOTIFICATION_TYPES", "Chọn ít nhất 1 loại khách");
    public static readonly Error CustomersRequired = Error.Validation("NOTIFICATION_CUSTOMERS",
        $"Nhập mã khách nhận thông báo (tối đa {NotificationRules.MaxCustomers} mã)");
    public static readonly Error ExpiresBeforePublish = Error.Validation("NOTIFICATION_EXPIRES", "Ngày hết hiện phải sau giờ đăng");
    public static readonly Error NotFound = Error.NotFound("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo");
    public static readonly Error EmailNotConfigured = Error.Validation("EMAIL_NOT_CONFIGURED",
        "Máy chủ chưa cấu hình tài khoản gửi email (Email:UserName / Email:Password)");
    public static readonly Error TestEmailInvalid = Error.Validation("NOTIFICATION_TEST_EMAIL",
        $"Nhập email nhận thử hợp lệ (tối đa {NotificationRules.MaxTestRecipients} địa chỉ, cách nhau bằng dấu phẩy)");

    public static Error TestEmailFailed(string message) => Error.BusinessRule("NOTIFICATION_TEST_EMAIL_FAILED", $"Gửi email thử không được: {message}");

    public static Error Item(int index, string message) => Error.Validation("NOTIFICATION_ITEM", $"Tin {index + 1}: {message}");

    public static Error UnknownCustomers(IEnumerable<string> codes) =>
        Error.Validation("NOTIFICATION_UNKNOWN_CUSTOMERS", $"Không tìm thấy khách: {string.Join(", ", codes)}");
}

/// <summary>Quy tắc thông báo — hàm thuần, có test.</summary>
internal static class NotificationRules
{
    /// <summary>Gmail cá nhân giới hạn ~500 người nhận / ngày — chừa lại cho email góp ý / hỗ trợ.</summary>
    public const int MaxEmailRecipients = 450;

    /// <summary>Số người nhận (BCC) mỗi email — Gmail giới hạn 100 / email.</summary>
    public const int EmailBatchSize = 50;

    public const int MaxItems = 10;
    public const int MaxCustomers = 500;

    /// <summary>Số địa chỉ tối đa khi gửi thử.</summary>
    public const int MaxTestRecipients = 5;

    /// <summary>Email nhận thử ("a@x.vn, b@y.vn") → danh sách hợp lệ; null nếu trống, có địa chỉ sai hoặc quá <see cref="MaxTestRecipients"/>.</summary>
    public static IReadOnlyList<string>? TestRecipients(string? value)
    {
        var parts = (value ?? "").Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (emails, _) = Recipients(parts, int.MaxValue);
        return parts.Length == 0 || emails.Count != parts.Distinct(StringComparer.OrdinalIgnoreCase).Count() || emails.Count > MaxTestRecipients ? null : emails;
    }

    public static Error? Validate(string? title, string? body, string? link = null)
    {
        var t = title?.Trim() ?? "";
        var b = body?.Trim() ?? "";
        if (t.Length is 0 or > Notification.TitleMaxLength) return NotificationErrors.TitleRequired;
        if (b.Length is 0 or > Notification.BodyMaxLength) return NotificationErrors.BodyRequired;
        if (!string.IsNullOrWhiteSpace(link) && !IsWebLink(link)) return NotificationErrors.Link;
        return null;
    }

    /// <summary>
    /// Tiêu đề thông báo + các tin: bỏ tin trống hoàn toàn; mỗi tin cần nội dung; có từ 2 tin thì mỗi tin cần tiêu đề;
    /// tối đa <see cref="MaxItems"/> tin, <see cref="NotificationItem.MaxImages"/> ảnh / tin; nút "Xem chi tiết" mở link http / https
    /// hoặc nội dung chi tiết (không cả hai). Nội dung có định dạng được lọc HTML; độ dài tính theo chữ (bỏ thẻ).
    /// Trả các tin đã chuẩn hóa (HTML đã lọc).
    /// </summary>
    public static Result<IReadOnlyList<NotificationItemInput>> ValidateContent(string? title, IReadOnlyList<NotificationItemInput>? items)
    {
        var t = title?.Trim() ?? "";
        if (t.Length is 0 or > Notification.TitleMaxLength) return NotificationErrors.TitleRequired;
        var list = (items ?? [])
            .Select(i => i with { Body = Clean(i.Body), Detail = Clean(i.Detail) })
            .Where(i => !string.IsNullOrWhiteSpace(i.Title) || RichText.ToPlainText(i.Body).Length > 0 || i.Images is { Count: > 0 }
                || !string.IsNullOrWhiteSpace(i.Link) || RichText.ToPlainText(i.Detail).Length > 0)
            .ToList();
        if (list.Count is 0 or > MaxItems) return NotificationErrors.ItemsRequired;
        for (var n = 0; n < list.Count; n++)
        {
            var item = list[n];
            var itemTitle = item.Title?.Trim() ?? "";
            var body = RichText.ToPlainText(item.Body);
            var detail = RichText.ToPlainText(item.Detail);
            if (itemTitle.Length > Notification.TitleMaxLength) return NotificationErrors.Item(n, $"tiêu đề tối đa {Notification.TitleMaxLength} ký tự");
            if (list.Count > 1 && itemTitle.Length == 0) return NotificationErrors.Item(n, "nhập tiêu đề tin (thông báo có nhiều tin)");
            if (body.Length is 0 or > NotificationItem.BodyTextMaxLength || (item.Body?.Length ?? 0) > NotificationItem.BodyHtmlMaxLength)
                return NotificationErrors.Item(n, $"nhập nội dung (tối đa {NotificationItem.BodyTextMaxLength} ký tự)");
            if ((item.Images?.Count ?? 0) > NotificationItem.MaxImages) return NotificationErrors.Item(n, $"tối đa {NotificationItem.MaxImages} ảnh");
            if (!string.IsNullOrWhiteSpace(item.Link) && !IsWebLink(item.Link))
                return NotificationErrors.Item(n, $"đường link phải bắt đầu bằng http:// hoặc https:// (tối đa {Notification.LinkMaxLength} ký tự)");
            if (detail.Length > NotificationItem.DetailTextMaxLength || (item.Detail?.Length ?? 0) > NotificationItem.DetailHtmlMaxLength)
                return NotificationErrors.Item(n, $"nội dung chi tiết tối đa {NotificationItem.DetailTextMaxLength} ký tự");
            if (!string.IsNullOrWhiteSpace(item.Link) && detail.Length > 0)
                return NotificationErrors.Item(n, "nút Xem chi tiết chỉ mở link hoặc nội dung chi tiết, không cả hai");
        }
        return list.Select(i => i with { Detail = RichText.ToPlainText(i.Detail).Length > 0 ? i.Detail : null }).ToList();
    }

    /// <summary>Có thẻ định dạng → lọc HTML; chữ thường giữ nguyên.</summary>
    private static string? Clean(string? value) => value is null ? null : RichText.IsRich(value) ? RichText.Sanitize(value).Trim() : value.Trim();

    /// <summary>Link tuyệt đối http / https (chặn javascript:, data:… vì khách bấm mở trực tiếp).</summary>
    public static bool IsWebLink(string value)
    {
        var v = value.Trim();
        return v.Length <= Notification.LinkMaxLength
            && Uri.TryCreate(v, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>Mã khách nhập tay ("KH1, kh2\nKH3") → danh sách không trùng (không phân biệt hoa thường), giữ thứ tự.</summary>
    public static IReadOnlyList<string> CustomerCodes(IEnumerable<string>? codes) =>
        (codes ?? []).SelectMany(c => c.Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Trạng thái hiển thị ở trang quản trị.</summary>
    public static string StatusOf(Notification n, DateTime now) => n.Status switch
    {
        NotificationStatus.Draft => "draft",
        NotificationStatus.Removed => "removed",
        _ when n.PublishAt > now => "scheduled",
        _ when n.ExpiresAt is { } end && end < now => "expired",
        _ => "live"
    };

    /// <summary>"Hiện đến ngày" → hết ngày đó (23:59:59).</summary>
    public static DateTime? EndOfDay(DateOnly? date) => date?.ToDateTime(new TimeOnly(23, 59, 59));

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

    /// <summary>Ảnh "/Upload/ThongBao/{tên}" → tên tệp (đường dẫn khác bị bỏ).</summary>
    public static IReadOnlyList<string> ImageNames(NotificationItem item) =>
        item.ImagePaths.Select(NotificationImageStore.NameOf).OfType<string>().ToList();

    public static IReadOnlyList<NotificationItemDto> Items(IEnumerable<NotificationItem> items) =>
        items.OrderBy(i => i.SortOrder).Select(i => new NotificationItemDto(i.Title, i.Body, ImageNames(i), i.Link, i.Detail)).ToList();

    /// <summary>Tổng dung lượng ảnh nhúng trong 1 email — Gmail giới hạn 25 MB / email (tính cả mã hóa base64 ~ +33%).</summary>
    public const int MaxInlineImageBytes = 15 * 1024 * 1024;

    /// <summary>
    /// Ảnh của các tin nhúng vào email (ContentId = tên tệp) theo thứ tự tin / ảnh, tối đa <see cref="MaxInlineImageBytes"/>;
    /// ảnh không còn trên đĩa hoặc vượt giới hạn thì bỏ (vẫn xem được trên portal).
    /// </summary>
    public static IReadOnlyList<EmailAttachment> InlineImages(Notification n, NotificationImageStore? store)
    {
        var list = new List<EmailAttachment>();
        if (store is null) return list;
        var total = 0L;
        foreach (var name in n.Items.OrderBy(i => i.SortOrder).SelectMany(ImageNames).Distinct())
        {
            if (store.Find(name) is not { } file) continue;
            var size = new FileInfo(file.Path).Length;
            if (total + size > MaxInlineImageBytes) continue;
            list.Add(new EmailAttachment(name, file.ContentType, File.ReadAllBytes(file.Path), ContentId: name));
            total += size;
        }
        return list;
    }

    /// <summary>
    /// Email thông báo gửi khách (chưa có người nhận — job gắn BCC theo lô). Mỗi tin là 1 khối kèm ảnh, nội dung có định dạng
    /// và nút riêng: link của tin, hoặc (tin có nội dung chi tiết) mở thông báo trên portal.
    /// Ảnh có trong <paramref name="inline"/> nhúng thẳng vào email (cid:) — không phụ thuộc máy chủ có cho tải ảnh từ ngoài hay không;
    /// ảnh không nhúng được thì bỏ.
    /// </summary>
    public static EmailMessage Email(Notification n, EmailCompany company, IReadOnlyList<EmailAttachment>? inline = null)
    {
        var embedded = (inline ?? []).Select(a => a.ContentId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var items = n.Items.OrderBy(i => i.SortOrder).ToList();
        var onPortal = company.Portal($"/notifications?id={n.Id.ToString(CultureInfo.InvariantCulture)}");
        var sections = items.Count == 0
            ? [new EmailSection(null, n.Body)]
            : items.Select(i => new EmailSection(
                    i.Title,
                    RichText.IsRich(i.Body) ? RichText.ToPlainText(i.Body) : i.Body,
                    Images: ImageNames(i).Where(embedded.Contains).Select(name => "cid:" + name).ToList(),
                    Link: i.Link is { } link ? new EmailLink("Xem chi tiết", link)
                        : i.Detail is not null && onPortal is not null ? new EmailLink("Xem chi tiết trên portal", onPortal) : null,
                    Html: RichText.IsRich(i.Body) ? i.Body : null))
                .ToList();
        var content = new EmailContent(
            Title: n.Title,
            Badge: n.Important ? "Thông báo quan trọng" : null,
            Meta: $"Việt An Express · {Time(n.PublishAt)}",
            Sections: sections,
            Button: items.Count == 0 && n.Link is { } single ? new EmailLink("Xem tại đây", single) : null,
            SecondaryLink: company.Portal("/notifications") is { } portal ? new EmailLink("Xem tất cả thông báo trên portal", portal) : null,
            FooterNote: "Bạn nhận email này vì có tài khoản trên portal Việt An Express. Email gửi tự động, vui lòng không trả lời.");
        return new EmailMessage(
            n.Important ? $"[Quan trọng] {n.Title} — Việt An Express" : $"{n.Title} — Việt An Express",
            EmailTemplate.PlainText(content, company),
            Attachments: inline is { Count: > 0 } ? inline : null,
            HtmlBody: EmailTemplate.Html(content, company));
    }
}

/// <summary>Truy vấn dùng chung: thông báo khách thấy và khách nhận thông báo.</summary>
internal static class NotificationQueries
{
    /// <summary>
    /// Thông báo riêng của khách + thông báo đã đăng, đang trong thời gian hiện, gửi tới khách
    /// (mọi khách / đúng loại khách / có trong danh sách chỉ định).
    /// </summary>
    public static IQueryable<Notification> VisibleTo(this IQueryable<Notification> q, IdentityDbContext db, long customerId, int? customerType, DateTime now)
    {
        var typeToken = customerType is { } type ? $",{type}," : null;
        var customerToken = Notification.Token(customerId);
        return q.Where(n => n.CustomerId == customerId
            || (n.CustomerId == null && n.Status == NotificationStatus.Published && n.PublishAt <= now && (n.ExpiresAt == null || n.ExpiresAt >= now)
                && (n.Audience == NotificationAudience.All
                    || (n.Audience == NotificationAudience.Types && typeToken != null && n.CustomerTypes != null && n.CustomerTypes.Contains(typeToken))
                    || (n.Audience == NotificationAudience.Customers && n.Recipients != null && n.Recipients.Contains(customerToken)))));
    }

    /// <summary>Khách có tài khoản portal (đã đặt mật khẩu).</summary>
    public static IQueryable<CustomerLogin> PortalCustomers(this IdentityDbContext db) =>
        db.Logins.Where(l => l.Password != null && l.Password != "");

    /// <summary>Khách nhận 1 thông báo quản trị đăng.</summary>
    public static IQueryable<long> AudienceOf(this IdentityDbContext db, Notification n)
    {
        var types = n.CustomerTypeIds.ToList();
        var ids = n.RecipientIds.ToList();
        return n.Audience switch
        {
            NotificationAudience.Types => db.PortalCustomers().Where(l => l.CustomerTypeId != null && types.Contains(l.CustomerTypeId.Value)).Select(l => l.CustomerId),
            NotificationAudience.Customers => db.Logins.Where(l => ids.Contains(l.CustomerId)).Select(l => l.CustomerId),
            _ => db.PortalCustomers().Select(l => l.CustomerId)
        };
    }

    /// <summary>Số khách có tài khoản portal theo loại (null = chưa phân loại).</summary>
    public static async Task<Dictionary<int, int>> TypeCountsAsync(this IdentityDbContext db, CancellationToken ct) =>
        (await db.PortalCustomers().Where(l => l.CustomerTypeId != null)
            .GroupBy(l => l.CustomerTypeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct))
        .ToDictionary(x => x.Key, x => x.Count);
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

    private async Task<IQueryable<Notification>> VisibleAsync(long customerId, CancellationToken ct)
    {
        var type = await db.Logins.AsNoTracking().Where(l => l.CustomerId == customerId).Select(l => l.CustomerTypeId).FirstOrDefaultAsync(ct);
        return db.Notifications.VisibleTo(db, customerId, type, VietnamTime.Now(clock));
    }

    public async Task<Result<NotificationListDto>> Handle(GetNotificationsQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var staff = StaffKey;
        var rows = await (await VisibleAsync(customerId, ct)).AsNoTracking()
            .OrderByDescending(n => n.Pinned && n.CustomerId == null)
            .ThenByDescending(n => n.PublishAt)
            .ThenByDescending(n => n.Id)
            .Take(ListLimit)
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

    public async Task<Result<bool>> Handle(MarkNotificationReadCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        if (!await (await VisibleAsync(customerId, ct)).AnyAsync(n => n.Id == c.Id, ct)) return NotificationErrors.NotFound;
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
        var unread = await (await VisibleAsync(customerId, ct))
            .OrderByDescending(n => n.Pinned && n.CustomerId == null)
            .ThenByDescending(n => n.PublishAt)
            .ThenByDescending(n => n.Id)
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

/// <summary>
/// Tạo (<see cref="Id"/> null) hoặc sửa thông báo. <see cref="Publish"/> false = lưu nháp; true = đăng vào <see cref="PublishAt"/>
/// (trống / đã qua = ngay). <see cref="CustomerTypes"/> dùng khi <see cref="Audience"/> = type, <see cref="CustomerCodes"/> khi = pick.
/// </summary>
internal sealed record SaveNotificationCommand(
    long? Id, string? Title, IReadOnlyList<NotificationItemInput>? Items, bool Important, bool Pinned, bool SendEmail,
    string? Audience, IReadOnlyList<int>? CustomerTypes, IReadOnlyList<string>? CustomerCodes,
    DateTime? PublishAt, DateOnly? ExpiresOn, bool Publish) : IRequest<Result<AdminNotificationDto>>;

internal sealed record GetAdminNotificationsQuery : IRequest<IReadOnlyList<AdminNotificationDto>>;

internal sealed record GetNotificationAudienceQuery : IRequest<NotificationAudienceDto>;

internal sealed record GetNotificationReadersQuery(long Id) : IRequest<Result<IReadOnlyList<NotificationReaderDto>>>;

/// <summary>Gỡ thông báo khỏi portal của khách (giữ lại ở quản trị, đăng lại được).</summary>
internal sealed record RemoveNotificationCommand(long Id) : IRequest<Result<AdminNotificationDto>>;

internal sealed record DeleteNotificationCommand(long Id) : IRequest<Result<bool>>;

/// <summary>Gửi thử email của nội dung đang soạn (chưa cần lưu) tới <see cref="To"/> — không ghi gì vào DB. Trả số địa chỉ đã gửi.</summary>
internal sealed record TestNotificationEmailCommand(string? To, string? Title, IReadOnlyList<NotificationItemInput>? Items, bool Important)
    : IRequest<Result<int>>;

internal sealed class AdminNotificationHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IEmailSender email, IBackgroundJobQueue jobs, NotificationImageStore images,
    TimeProvider clock, IConfiguration config) :
    IRequestHandler<SaveNotificationCommand, Result<AdminNotificationDto>>,
    IRequestHandler<TestNotificationEmailCommand, Result<int>>,
    IRequestHandler<GetAdminNotificationsQuery, IReadOnlyList<AdminNotificationDto>>,
    IRequestHandler<GetNotificationAudienceQuery, NotificationAudienceDto>,
    IRequestHandler<GetNotificationReadersQuery, Result<IReadOnlyList<NotificationReaderDto>>>,
    IRequestHandler<RemoveNotificationCommand, Result<AdminNotificationDto>>,
    IRequestHandler<DeleteNotificationCommand, Result<bool>>
{
    private const int AdminListLimit = 200;
    private const int ReaderLimit = 500;

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

        var items = new List<NotificationItem>();
        foreach (var input in content.Value)
        {
            var paths = (input.Images ?? []).Select(NotificationImageStore.StoredPath).ToList();
            if (paths.Any(p => !images.Exists(p))) return NotificationErrors.ImageNotFound;
            items.Add(new NotificationItem(input.Title, input.Body!, paths, input.Link, input.Detail));
        }

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
        return (await ToDtosAsync([notification], ct))[0];
    }

    public async Task<Result<int>> Handle(TestNotificationEmailCommand c, CancellationToken ct)
    {
        if (NotificationRules.TestRecipients(c.To) is not { } to) return NotificationErrors.TestEmailInvalid;
        var content = NotificationRules.ValidateContent(c.Title, c.Items);
        if (content.IsFailure) return content.Error;
        if (!email.IsConfigured) return NotificationErrors.EmailNotConfigured;

        var items = new List<NotificationItem>();
        foreach (var input in content.Value)
        {
            var paths = (input.Images ?? []).Select(NotificationImageStore.StoredPath).ToList();
            if (paths.Any(p => !images.Exists(p))) return NotificationErrors.ImageNotFound;
            items.Add(new NotificationItem(input.Title, input.Body!, paths, input.Link, input.Detail));
        }

        // Bản tạm trong bộ nhớ (không thêm vào DbContext) — dùng chung mẫu email với lần gửi thật.
        var draft = Notification.Draft(user.UserName ?? "", VietnamTime.Now(clock));
        draft.SetContent(c.Title!, items, c.Important, false, null, NotificationAudience.All, [], []);
        var message = NotificationRules.Email(draft, EmailCompany.From(config), NotificationRules.InlineImages(draft, images));
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

    public async Task<IReadOnlyList<AdminNotificationDto>> Handle(GetAdminNotificationsQuery q, CancellationToken ct)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == null)
            .OrderByDescending(n => n.Id)
            .Take(AdminListLimit)
            .ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

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

    public async Task<Result<AdminNotificationDto>> Handle(RemoveNotificationCommand c, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == c.Id && n.CustomerId == null, ct);
        if (notification is null) return NotificationErrors.NotFound;
        notification.Remove();
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([notification], ct))[0];
    }

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

    private async Task<IReadOnlyList<AdminNotificationDto>> ToDtosAsync(IReadOnlyList<Notification> rows, CancellationToken ct)
    {
        var now = VietnamTime.Now(clock);
        var ids = rows.Select(n => n.Id).ToList();
        var reads = await db.NotificationReads.AsNoTracking()
            .Where(r => ids.Contains(r.NotificationId))
            .GroupBy(r => r.NotificationId)
            .Select(g => new { Id = g.Key, Accounts = g.Count(), Customers = g.Select(r => r.CustomerId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Id, ct);
        var total = await db.PortalCustomers().CountAsync(ct);
        var typeCounts = await db.TypeCountsAsync(ct);
        var recipientIds = rows.SelectMany(n => n.RecipientIds).Distinct().ToList();
        var codes = recipientIds.Count == 0 ? [] : (await customers.GetByIdsAsync(recipientIds, ct)).ToDictionary(p => p.Id, p => p.Code);

        return rows.Select(n =>
        {
            var types = n.CustomerTypeIds;
            var recipients = n.Audience switch
            {
                NotificationAudience.Types => types.Sum(t => typeCounts.GetValueOrDefault(t)),
                NotificationAudience.Customers => n.RecipientIds.Count,
                _ => total
            };
            var read = reads.GetValueOrDefault(n.Id);
            return new AdminNotificationDto(
                n.Id.ToString(CultureInfo.InvariantCulture), n.Title, NotificationRules.StatusOf(n, now), n.Important, n.Pinned,
                n.PublishAt.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
                n.ExpiresAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                NotificationRules.Time(n.CreatedAt), n.CreatedBy,
                n.Audience, types, n.RecipientIds.Select(id => codes.GetValueOrDefault(id) ?? id.ToString(CultureInfo.InvariantCulture)).ToList(),
                recipients, read?.Customers ?? 0, read?.Accounts ?? 0,
                n.EmailStatus, n.EmailRecipients, n.EmailSent, n.EmailError,
                n.Items.Count > 0 ? NotificationRules.Items(n.Items) : [new NotificationItemDto(null, n.Body, [], n.Link)]);
        }).ToList();
    }
}

/// <summary>
/// Gửi email thông báo ở nền: người nhận = email hồ sơ của khách thuộc đối tượng nhận (có tài khoản portal) + email tài khoản con đang hoạt động
/// của các khách đó, tối đa <see cref="NotificationRules.MaxEmailRecipients"/> địa chỉ, gửi BCC theo lô. Ghi kết quả vào dbo.TThongBao.
/// </summary>
internal static class NotificationEmailJob
{
    public static void Enqueue(IBackgroundJobQueue jobs, long notificationId) =>
        jobs.Enqueue($"email thông báo #{notificationId}", (services, token) => RunAsync(services, notificationId, token));

    public static async Task RunAsync(IServiceProvider services, long notificationId, CancellationToken ct)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var customers = services.GetRequiredService<ICustomersApi>();
        var email = services.GetRequiredService<IEmailSender>();
        var company = EmailCompany.From(services.GetRequiredService<IConfiguration>());
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(NotificationEmailJob));

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, ct);
        if (notification is null || notification.Status != NotificationStatus.Published) return;

        var customerIds = await db.AudienceOf(notification).Distinct().ToListAsync(ct);
        var staffEmails = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.IsActive && s.Email != null && customerIds.Contains(s.CustomerId))
            .Select(s => s.Email)
            .ToListAsync(ct);
        var profiles = await customers.GetByIdsAsync(customerIds, ct);
        var (recipients, skipped) = NotificationRules.Recipients(profiles.Select(p => p.Email).Concat(staffEmails), NotificationRules.MaxEmailRecipients);

        var sent = 0;
        string? error = skipped > 0 ? $"Vượt giới hạn {NotificationRules.MaxEmailRecipients} người nhận / lần — {skipped} địa chỉ chưa gửi" : null;
        var template = NotificationRules.Email(notification, company, NotificationRules.InlineImages(notification, services.GetService<NotificationImageStore>()));
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

    /// <summary>Thông báo hẹn giờ có email đã tới giờ đăng → chuyển sang đang gửi và xếp việc gửi. Trả số thông báo đã xếp.</summary>
    public static async Task<int> StartDueAsync(IdentityDbContext db, IBackgroundJobQueue jobs, TimeProvider clock, CancellationToken ct)
    {
        var now = VietnamTime.Now(clock);
        var due = await db.Notifications
            .Where(n => n.Status == NotificationStatus.Published && n.EmailStatus == NotificationEmailStatus.Scheduled && n.PublishAt <= now)
            .ToListAsync(ct);
        var started = due.Where(n => n.StartScheduledEmail(now)).ToList();
        if (started.Count == 0) return 0;
        await db.SaveChangesAsync(ct);
        foreach (var n in started) Enqueue(jobs, n.Id);
        return started.Count;
    }
}
