using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>Yêu cầu hỗ trợ &amp; sự cố — tên trường khớp frontend (<c>features/troubles/types.ts</c>).</summary>
internal sealed record TroubleDto(
    string Id, string Kind, string Bill, string Cnee, string Ct, string Type, string? Subject, string Lv, string Desc, string Req, string Contact,
    string Date, string Status, string UpdatedAt);

/// <summary>Yêu cầu ở trang quản trị: kèm khách gửi.</summary>
/// <param name="ManagerId">Nhân viên Việt An phụ trách khách (TCustomer.Staff_ID → USERS.ID) — null khi chưa phân công.</param>
/// <param name="Email">Email người gửi nhập (TContact.Email) — lời nhắn từ trang chủ chỉ liên hệ lại được qua đây / SĐT.</param>
internal sealed record AdminTroubleDto(TroubleDto Ticket, string CustomerCode, string CompanyName, bool IsStaff, int? ManagerId = null, string? ManagerName = null,
    string? Email = null);

/// <summary>
/// <c>kind</c>: ho_tro (hỗ trợ &amp; sự cố — mặc định; su_co cũ cũng nhận như ho_tro) / gop_y. <c>bill</c> không bắt buộc — có thì phải là
/// vận đơn của khách; <c>subject</c> không bắt buộc (trống thì tiêu đề = vấn đề).
/// </summary>
internal sealed record NewTroubleRequest(
    string? Kind, string? Bill, string? Cnee, string? Ct, string? Type, string? Subject, string? Lv, string? Desc, string? Req, string? Contact);

internal static class TroubleErrors
{
    public static readonly Error Kind = Error.Validation("TROUBLE_KIND", "Chọn loại yêu cầu");
    public static readonly Error Bill = Error.Validation("TROUBLE_BILL", "Mã vận đơn không hợp lệ");
    public static readonly Error Subject = Error.Validation("TROUBLE_SUBJECT", $"Tiêu đề tối đa {Trouble.SubjectMaxLength} ký tự");
    public static readonly Error BillNotFound = Error.Validation("TROUBLE_BILL_NOT_FOUND", "Không tìm thấy vận đơn này trong đơn hàng của bạn");
    public static readonly Error Type = Error.Validation("TROUBLE_TYPE", "Chọn vấn đề cần hỗ trợ");
    public static readonly Error FeedbackTopic = Error.Validation("TROUBLE_FEEDBACK_TOPIC", "Chọn nội dung góp ý");
    public static readonly Error Priority = Error.Validation("TROUBLE_PRIORITY", "Mức độ không hợp lệ");
    public static readonly Error Description = Error.Validation("TROUBLE_DESC", $"Nội dung từ {Trouble.DescriptionMinLength} đến {Trouble.DescriptionMaxLength} ký tự");
    public static readonly Error Requester = Error.Validation("TROUBLE_REQUESTER", "Nhập người yêu cầu");
    public static readonly Error Contact = Error.Validation("TROUBLE_CONTACT", "Nhập SĐT hoặc email liên hệ");
    public static readonly Error FileTooLarge = Error.Validation("TROUBLE_FILE_TOO_LARGE", "Tệp đính kèm tối đa 10 MB");
    public static readonly Error FileType = Error.Validation("TROUBLE_FILE_TYPE", "Chỉ nhận ảnh, PDF, Excel, CSV hoặc Word");
    public static readonly Error Note = Error.Validation("TROUBLE_NOTE", "Ghi chú tối đa 4000 ký tự");
    public static readonly Error Status = Error.Validation("TROUBLE_STATUS", "Trạng thái không hợp lệ");
    public static readonly Error NothingToSend = Error.Validation("TROUBLE_EMPTY", "Nhập ghi chú hoặc đổi trạng thái");
    public static readonly Error NotFound = Error.NotFound("TROUBLE_NOT_FOUND", "Không tìm thấy yêu cầu");
    public static readonly Error ContactNote = Error.Validation("TROUBLE_CONTACT_NOTE", "Lời nhắn từ trang chủ chưa có tài khoản để nhận thông báo — gọi hoặc email khách rồi đổi trạng thái");
}

/// <summary>Quy tắc yêu cầu hỗ trợ &amp; sự cố — hàm thuần, có test.</summary>
internal static class TroubleRules
{
    /// <summary>
    /// Nội dung góp ý — đúng 4 mục ô "Chọn nội dung góp ý" của form "Yêu cầu" (Bill Online cũ), lưu TContact.Title (KeyWord = "Feedback") như form cũ.
    /// Khách không chọn (client cũ) → "Khác".
    /// </summary>
    public static readonly IReadOnlyList<string> FeedbackTopics = ["Chỉnh sửa phần mềm", "Cập nhật thông tin", "Góp ý nâng cấp", "Khác"];

    public const string FeedbackType = "Khác";
    public const int NoteMaxLength = 4000;

    /// <summary>Hỗ trợ và sự cố là 1 loại (lưu "Portal - Hỗ trợ"); su_co của frontend cũ cũng gộp vào đây.</summary>
    public static string KindOf(NewTroubleRequest r) =>
        r.Kind?.Trim() is { Length: > 0 } k && k != TroubleKind.Incident ? k : TroubleKind.Support;

    public static Error? Validate(NewTroubleRequest r, TroubleFile? file = null)
    {
        var kind = KindOf(r);
        if (!TroubleKind.All.Contains(kind) || kind == TroubleKind.Incident) return TroubleErrors.Kind;
        var support = kind == TroubleKind.Support;

        if ((r.Bill?.Trim().Length ?? 0) > Trouble.BillMaxLength) return TroubleErrors.Bill;
        if (support && (r.Type?.Trim().Length ?? 0) is 0 or > Trouble.TypeMaxLength) return TroubleErrors.Type;
        if (!support && r.Type?.Trim() is { Length: > 0 } topic && !FeedbackTopics.Contains(topic)) return TroubleErrors.FeedbackTopic;
        if ((r.Subject?.Trim().Length ?? 0) > Trouble.SubjectMaxLength) return TroubleErrors.Subject;
        if (r.Lv is not null && !TroublePriority.All.Contains(r.Lv)) return TroubleErrors.Priority;
        if ((r.Desc?.Trim().Length ?? 0) is < Trouble.DescriptionMinLength or > Trouble.DescriptionMaxLength) return TroubleErrors.Description;
        if ((r.Req?.Trim().Length ?? 0) > Trouble.RequesterMaxLength) return TroubleErrors.Requester;
        var contact = r.Contact?.Trim().Length ?? 0;
        if ((support && contact == 0) || contact > Trouble.ContactMaxLength) return TroubleErrors.Contact;
        if (file is not null)
        {
            if (file.Data.Length is 0 or > Trouble.FileMaxBytes) return TroubleErrors.FileTooLarge;
            if (!Trouble.FileExtensions.Contains(Path.GetExtension(file.Name))) return TroubleErrors.FileType;
        }
        return null;
    }

    public static string Time(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static DateTime ParseTime(string value) =>
        DateTime.TryParseExact(value, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : DateTime.MinValue;

    public static TroubleDto ToDto(Trouble t) => new(
        t.Code, t.Kind, t.Bill ?? "", t.ConsigneeName ?? "", t.Country ?? "", t.Topic, t.Subject, t.Priority ?? "low",
        t.Description, t.Requester, t.Contact, Time(t.CreatedAt), t.Status, Time(t.UpdatedAt));

    /// <summary>Tên hiển thị: "Sự cố TRB-12" / "Yêu cầu hỗ trợ TRB-13" / "Góp ý TRB-14".</summary>
    public static string Title(Trouble t) => $"{TroubleKind.Label(t.Kind)} {t.Code}";

    /// <summary>Dòng đầu nội dung thông báo / email: vận đơn (sự cố) hoặc tiêu đề (hỗ trợ) kèm loại / chủ đề.</summary>
    public static string Summary(Trouble t) =>
        t.Bill is { Length: > 0 } bill ? $"Mã vận đơn: {bill} · {t.Topic}"
        : t.Subject is { Length: > 0 } subject ? $"{subject} · {t.Topic}"
        : t.Topic;

    /// <summary>Thông báo riêng cho khách khi Việt An đổi trạng thái / gửi ghi chú (Trung tâm thông báo, nút "Xem tại đây" mở đúng yêu cầu).</summary>
    public static Notification CustomerNotice(Trouble t, string? note, bool statusChanged, string author, string? link, DateTime now)
    {
        var title = statusChanged
            ? $"{Title(t)}: {TroubleStatus.Label(t.Status)}"
            : $"Việt An đã phản hồi {TroubleKind.Label(t.Kind).ToLowerInvariant()} {t.Code}";
        var body = $"{Summary(t)}\n\n"
            + (string.IsNullOrWhiteSpace(note) ? $"Trạng thái đã chuyển sang \"{TroubleStatus.Label(t.Status)}\"." : note.Trim());
        return Notification.Create(title, body, important: false, sendEmail: false, author, now, link, t.CustomerId);
    }

    /// <summary>Email báo yêu cầu mới về hộp thư Việt An — kèm tệp khách đính kèm (tệp không lưu database).</summary>
    public static EmailMessage InboxEmail(Trouble t, CustomerSummary? customer, string? userName, TroubleFile? file, EmailCompany company)
    {
        var replyTo = SupportRules.ReplyTo(t.Contact, customer?.Email);
        var incident = !string.IsNullOrEmpty(t.Bill); // có vận đơn → kèm mức độ + thông tin vận đơn
        var heading = t.Subject is { Length: > 0 } subject ? subject : t.Topic;
        var content = new EmailContent(
            Title: $"{Title(t)}: {heading}",
            Badge: incident ? t.Priority switch { "high" => "Rất gấp", "mid" => "Gấp", _ => "Thường" } : $"{TroubleKind.Label(t.Kind)} · {t.Topic}",
            Meta: $"{(incident ? $"Vận đơn {t.Bill} · " : "")}{t.CompanyName} ({customer?.Code ?? "?"}) · {Time(t.CreatedAt)}",
            Sections:
            [
                new EmailSection("Nội dung", t.Description),
                .. incident
                    ? [new EmailSection("Vận đơn", Fields:
                        [
                            new EmailField("Mã vận đơn", t.Bill ?? "—"),
                            new EmailField("Người nhận", t.ConsigneeName ?? "—"),
                            new EmailField("Quốc gia", t.Country ?? "—"),
                            new EmailField("Người yêu cầu", t.Requester)
                        ])]
                    : Array.Empty<EmailSection>(),
                new EmailSection("Thông tin khách", Fields: SupportRules.CustomerFields(customer, userName, t.StaffId is not null, t.Contact))
            ],
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Xử lý trong trang quản trị", admin) : null,
            FooterNote: SupportRules.ReplyNote(replyTo, t.Contact));
        return new EmailMessage(
            $"[{TroubleKind.Label(t.Kind)} portal] {t.Code} — {heading}{(incident ? $" — {t.Bill}" : "")} ({customer?.Code ?? "?"})",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            Attachments: file is null ? null : [new EmailAttachment(file.Name, file.ContentType, file.Data)],
            HtmlBody: EmailTemplate.Html(content, company));
    }

    public static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();
}

// ---------------- Khách ----------------

internal sealed record GetTroublesQuery : IRequest<Result<IReadOnlyList<TroubleDto>>>;

internal sealed record CreateTroubleCommand(NewTroubleRequest Request, TroubleFile? File = null) : IRequest<Result<TroubleDto>>;

/// <summary>
/// Khách gửi / xem yêu cầu — 1 dòng dbo.TContact mỗi yêu cầu (như form "Yêu cầu" của Bill Online cũ); CS xử lý ở phần mềm cũ / trang /admin,
/// đổi trạng thái thì khách nhận thông báo. Không dùng bảng Order_Trouble (sổ theo dõi đơn trễ nội bộ của nhân viên).
/// </summary>
internal sealed class CustomerTroubleHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IShipmentsApi shipments, IBackgroundJobQueue jobs,
    EmailCompany company, TimeProvider clock) :
    IRequestHandler<GetTroublesQuery, Result<IReadOnlyList<TroubleDto>>>,
    IRequestHandler<CreateTroubleCommand, Result<TroubleDto>>
{
    private const int ListLimit = 200;

    /// <summary>Tài khoản chính thấy mọi yêu cầu của công ty; tài khoản con chỉ thấy yêu cầu mình gửi.</summary>
    private IQueryable<Trouble> Mine(long customerId)
    {
        var query = db.Troubles.Where(t => t.CustomerId == customerId);
        return user.StaffId is { } staffId ? query.Where(t => t.StaffId == staffId) : query;
    }

    public async Task<Result<IReadOnlyList<TroubleDto>>> Handle(GetTroublesQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var rows = await Mine(customerId).AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        return rows.Select(TroubleRules.ToDto).ToList();
    }

    public async Task<Result<TroubleDto>> Handle(CreateTroubleCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var r = c.Request;
        if (TroubleRules.Validate(r, c.File) is { } error) return error;
        var kind = TroubleRules.KindOf(r);

        // Có mã vận đơn → phải là vận đơn của khách; lấy người nhận / nước từ vận đơn nếu khách không nhập.
        string? bill = null, consignee = null, country = null;
        if (kind == TroubleKind.Support && r.Bill?.Trim() is { Length: > 0 } typed)
        {
            var found = await shipments.FindBillAsync(customerId, typed, ct);
            if (found is null) return TroubleErrors.BillNotFound;
            bill = found.Bill;
            consignee = TroubleRules.Cut(string.IsNullOrWhiteSpace(r.Cnee) ? found.ConsigneeName : r.Cnee, Trouble.NameMaxLength);
            country = TroubleRules.Cut(string.IsNullOrWhiteSpace(r.Ct) ? found.ConsigneeCountry : r.Ct, Trouble.CountryMaxLength);
        }

        var customer = await customers.GetByIdAsync(customerId, ct);
        var userName = user.UserName ?? "";
        var contact = r.Contact?.Trim() ?? "";
        var trouble = Trouble.Create(kind, customerId, customer?.CompanyName ?? "", user.StaffId,
            bill, consignee, country,
            kind == TroubleKind.Feedback ? TroubleKind.FeedbackKeyWord : r.Type!.Trim(),
            kind == TroubleKind.Feedback ? (string.IsNullOrWhiteSpace(r.Type) ? TroubleRules.FeedbackType : r.Type.Trim())
                : string.IsNullOrWhiteSpace(r.Subject) ? null : r.Subject.Trim(),
            r.Lv ?? "low", r.Desc!.Trim(),
            string.IsNullOrWhiteSpace(r.Req) ? userName : r.Req.Trim(),
            contact, SupportRules.IsEmail(contact) ? contact : null, VietnamTime.Now(clock));
        var file = c.File is { } f ? f with { Name = SupportRules.SafeFileName(f.Name) } : null;
        if (file is not null) trouble.NoteAttachment(file.Name);
        db.Troubles.Add(trouble);
        await db.SaveChangesAsync(ct);
        // Báo về hộp thư Việt An ở nền (kèm tệp) — yêu cầu đã lưu ở dbo.TContact, xử lý ở phần mềm cũ / trang /admin.
        jobs.EnqueueEmail($"email {trouble.Code}", TroubleRules.InboxEmail(trouble, customer, userName, file, company));
        return TroubleRules.ToDto(trouble);
    }
}

// ---------------- Quản trị Việt An ----------------

/// <param name="ManagerId">Chỉ khách do nhân viên này phụ trách (USERS.ID) — cho CMS khi nhân viên thường đăng nhập; null = mọi khách.</param>
internal sealed record GetAdminTroublesQuery(int? ManagerId = null) : IRequest<IReadOnlyList<AdminTroubleDto>>;

/// <summary>Việt An đổi trạng thái và / hoặc gửi ghi chú → thông báo riêng cho khách (ghi chú chỉ nằm trong thông báo).</summary>
internal sealed record AdminReplyTroubleCommand(string Id, string? Reply, string? Status) : IRequest<Result<AdminTroubleDto>>;

internal sealed class AdminTroubleHandlers(IdentityDbContext db, ICurrentUser user, ICustomersApi customers, EmailCompany company, TimeProvider clock) :
    IRequestHandler<GetAdminTroublesQuery, IReadOnlyList<AdminTroubleDto>>,
    IRequestHandler<AdminReplyTroubleCommand, Result<AdminTroubleDto>>
{
    private const int ListLimit = 300;

    public async Task<IReadOnlyList<AdminTroubleDto>> Handle(GetAdminTroublesQuery q, CancellationToken ct)
    {
        var troubles = db.Troubles.AsNoTracking();
        if (q.ManagerId is { } managerId)
        {
            var mine = (await customers.GetCustomerIdsOfManagerAsync(managerId, ct)).ToList();
            troubles = troubles.Where(t => mine.Contains(t.CustomerId));
        }
        var rows = await troubles
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        var ids = rows.Select(t => t.CustomerId).Distinct().ToList();
        var found = (await customers.GetByIdsAsync(ids, ct)).ToDictionary(c => c.Id);
        var managers = await customers.GetAccountManagersAsync(ids, ct);
        return rows.Select(t => WithManager(ToDto(t, found.GetValueOrDefault(t.CustomerId)?.Code), managers.GetValueOrDefault(t.CustomerId))).ToList();
    }

    public async Task<Result<AdminTroubleDto>> Handle(AdminReplyTroubleCommand c, CancellationToken ct)
    {
        if (c.Status is { } status && !TroubleStatus.All.Contains(status)) return TroubleErrors.Status;
        if ((c.Reply?.Trim().Length ?? 0) > TroubleRules.NoteMaxLength) return TroubleErrors.Note;
        if (Trouble.ParseCode(c.Id) is not { } id) return TroubleErrors.NotFound;
        var trouble = await db.Troubles.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (trouble is null) return TroubleErrors.NotFound;

        var now = VietnamTime.Now(clock);
        // Lời nhắn từ trang chủ: khách chưa có tài khoản → chỉ đổi trạng thái, không tạo thông báo.
        var isContact = trouble.Kind == TroubleKind.Contact;
        if (isContact && !string.IsNullOrWhiteSpace(c.Reply)) return TroubleErrors.ContactNote;
        var changed = c.Status is { } next && trouble.ChangeStatus(next, now);
        if (!changed && string.IsNullOrWhiteSpace(c.Reply)) return TroubleErrors.NothingToSend;
        if (!isContact) db.Notifications.Add(TroubleRules.CustomerNotice(trouble, c.Reply, changed, user.UserName ?? "CS Việt An",
            company.Portal($"/troubles?id={trouble.Code}"), now));
        await db.SaveChangesAsync(ct);
        var customer = await customers.GetByIdAsync(trouble.CustomerId, ct);
        return ToDto(trouble, customer?.Code);
    }

    private static AdminTroubleDto WithManager(AdminTroubleDto dto, AccountManager? manager) =>
        manager is null ? dto : dto with { ManagerId = manager.Id, ManagerName = manager.FullName };

    private static AdminTroubleDto ToDto(Trouble t, string? customerCode) =>
        new(TroubleRules.ToDto(t), customerCode ?? "", t.CompanyName, t.StaffId is not null, Email: t.Email);
}
