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

/// <summary>1 lượt trao đổi: <c>from</c> = "customer" | "vietan"; <c>status</c> = trạng thái mới nếu lượt này đổi trạng thái.</summary>
internal sealed record TroubleMessageDto(string From, string Author, string Message, string Date, string? Status);

/// <summary>Sự cố — tên trường khớp frontend (<c>features/troubles/types.ts</c>).</summary>
internal sealed record TroubleDto(
    string Id, string Bill, string Cnee, string Ct, string Type, string Lv, string Desc, string Req, string Contact,
    string Date, string Status, string? Reply, IReadOnlyList<TroubleMessageDto> Messages);

/// <summary>Sự cố ở trang quản trị: kèm khách / người báo.</summary>
internal sealed record AdminTroubleDto(TroubleDto Ticket, string CustomerCode, string CompanyName, string UserName, bool IsStaff, string UpdatedAt);

internal sealed record NewTroubleRequest(string? Bill, string? Cnee, string? Ct, string? Type, string? Lv, string? Desc, string? Req, string? Contact);

internal static class TroubleErrors
{
    public static readonly Error Bill = Error.Validation("TROUBLE_BILL", "Nhập mã vận đơn");
    public static readonly Error BillNotFound = Error.Validation("TROUBLE_BILL_NOT_FOUND", "Không tìm thấy vận đơn này trong đơn hàng của bạn");
    public static readonly Error Type = Error.Validation("TROUBLE_TYPE", "Chọn loại sự cố");
    public static readonly Error Priority = Error.Validation("TROUBLE_PRIORITY", "Mức độ không hợp lệ");
    public static readonly Error Description = Error.Validation("TROUBLE_DESC", $"Mô tả từ 10 đến {Trouble.DescriptionMaxLength} ký tự");
    public static readonly Error Requester = Error.Validation("TROUBLE_REQUESTER", "Nhập người yêu cầu");
    public static readonly Error Contact = Error.Validation("TROUBLE_CONTACT", "Nhập SĐT hoặc email liên hệ");
    public static readonly Error Reply = Error.Validation("TROUBLE_REPLY", $"Nhập nội dung (tối đa {TroubleMessage.MessageMaxLength} ký tự)");
    public static readonly Error Status = Error.Validation("TROUBLE_STATUS", "Trạng thái không hợp lệ");
    public static readonly Error Closed = Error.BusinessRule("TROUBLE_CLOSED", "Sự cố đã xử lý xong — vui lòng báo sự cố mới nếu cần");
    public static readonly Error NothingToSend = Error.Validation("TROUBLE_EMPTY", "Nhập phản hồi hoặc đổi trạng thái");
    public static readonly Error NotFound = Error.NotFound("TROUBLE_NOT_FOUND", "Không tìm thấy sự cố");
}

/// <summary>Quy tắc sự cố — hàm thuần, có test.</summary>
internal static class TroubleRules
{
    public static Error? Validate(NewTroubleRequest r)
    {
        if ((r.Bill?.Trim().Length ?? 0) is 0 or > Trouble.BillMaxLength) return TroubleErrors.Bill;
        if ((r.Type?.Trim().Length ?? 0) is 0 or > Trouble.TypeMaxLength) return TroubleErrors.Type;
        if (r.Lv is null || !TroublePriority.All.Contains(r.Lv)) return TroubleErrors.Priority;
        if ((r.Desc?.Trim().Length ?? 0) is < 10 or > Trouble.DescriptionMaxLength) return TroubleErrors.Description;
        if ((r.Req?.Trim().Length ?? 0) is 0 or > Trouble.RequesterMaxLength) return TroubleErrors.Requester;
        if ((r.Contact?.Trim().Length ?? 0) is 0 or > Trouble.ContactMaxLength) return TroubleErrors.Contact;
        return null;
    }

    public static string Time(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static TroubleDto ToDto(Trouble t)
    {
        var messages = t.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
        return new TroubleDto(
            t.Code, t.Bill, t.ConsigneeName ?? "", t.Country ?? "", t.Type, t.Priority, t.Description, t.Requester, t.Contact,
            Time(t.CreatedAt), t.Status,
            messages.LastOrDefault(m => m.FromVietAn && m.Message.Length > 0)?.Message,
            messages.Select(m => new TroubleMessageDto(m.FromVietAn ? "vietan" : "customer", m.Author, m.Message, Time(m.CreatedAt), m.NewStatus)).ToList());
    }

    /// <summary>Thông báo riêng cho khách khi Việt An trả lời / đổi trạng thái (hiện ở Trung tâm thông báo, nút "Xem tại đây" mở trang Sự cố).</summary>
    public static Notification CustomerNotice(Trouble t, string? reply, bool statusChanged, string author, string? link, DateTime now)
    {
        var title = statusChanged
            ? $"Sự cố {t.Code}: {TroubleStatus.Label(t.Status)}"
            : $"Việt An đã phản hồi sự cố {t.Code}";
        var body = $"Mã vận đơn: {t.Bill} · {t.Type}\n\n"
            + (string.IsNullOrWhiteSpace(reply) ? $"Trạng thái sự cố đã chuyển sang \"{TroubleStatus.Label(t.Status)}\"." : reply.Trim());
        return Notification.Create(title, body, important: false, sendEmail: false, author, now, link, t.CustomerId);
    }

    /// <summary>Email báo sự cố mới về hộp thư Việt An.</summary>
    public static EmailMessage InboxEmail(Trouble t, CustomerSummary? customer, EmailCompany company)
    {
        var replyTo = SupportRules.ReplyTo(t.Contact, customer?.Email);
        var content = new EmailContent(
            Title: $"Sự cố {t.Code}: {t.Type}",
            Badge: t.Priority switch { "high" => "Rất gấp", "mid" => "Gấp", _ => "Thường" },
            Meta: $"Vận đơn {t.Bill} · {t.CompanyName} ({t.CustomerCode}) · {Time(t.CreatedAt)}",
            Sections:
            [
                new EmailSection("Mô tả", t.Description),
                new EmailSection("Vận đơn", Fields:
                [
                    new EmailField("Mã vận đơn", t.Bill),
                    new EmailField("Người nhận", t.ConsigneeName ?? "—"),
                    new EmailField("Quốc gia", t.Country ?? "—"),
                    new EmailField("Người yêu cầu", t.Requester)
                ]),
                new EmailSection("Thông tin khách", Fields: SupportRules.CustomerFields(customer, t.SenderUserName, t.StaffId is not null, t.Contact))
            ],
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Xử lý trong trang quản trị", admin) : null,
            FooterNote: SupportRules.ReplyNote(replyTo, t.Contact));
        return new EmailMessage(
            $"[Sự cố portal] {t.Code} — {t.Type} — {t.Bill} ({t.CustomerCode})",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            HtmlBody: EmailTemplate.Html(content, company));
    }
}

// ---------------- Khách ----------------

internal sealed record GetTroublesQuery : IRequest<Result<IReadOnlyList<TroubleDto>>>;

internal sealed record CreateTroubleCommand(NewTroubleRequest Request) : IRequest<Result<TroubleDto>>;

/// <summary>Khách gửi thêm thông tin; <c>Status</c> chỉ nhận "done" (khách tự đóng sự cố).</summary>
internal sealed record ReplyTroubleCommand(string Id, string? Reply, string? Status) : IRequest<Result<TroubleDto>>;

internal sealed class CustomerTroubleHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IShipmentsApi shipments, IBackgroundJobQueue jobs,
    EmailCompany company, TimeProvider clock) :
    IRequestHandler<GetTroublesQuery, Result<IReadOnlyList<TroubleDto>>>,
    IRequestHandler<CreateTroubleCommand, Result<TroubleDto>>,
    IRequestHandler<ReplyTroubleCommand, Result<TroubleDto>>
{
    private const int ListLimit = 200;

    /// <summary>Tài khoản chính thấy mọi sự cố của công ty; tài khoản con chỉ thấy sự cố mình báo.</summary>
    private IQueryable<Trouble> Mine(long customerId)
    {
        var query = db.Troubles.Where(t => t.CustomerId == customerId);
        return user.StaffId is { } staffId ? query.Where(t => t.StaffId == staffId) : query;
    }

    public async Task<Result<IReadOnlyList<TroubleDto>>> Handle(GetTroublesQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return FeedbackErrors.NotLoggedIn;
        var rows = await Mine(customerId).AsNoTracking().Include(t => t.Messages)
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        return rows.Select(TroubleRules.ToDto).ToList();
    }

    public async Task<Result<TroubleDto>> Handle(CreateTroubleCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return FeedbackErrors.NotLoggedIn;
        var r = c.Request;
        if (TroubleRules.Validate(r) is { } error) return error;
        var bill = await shipments.FindBillAsync(customerId, r.Bill!, ct);
        if (bill is null) return TroubleErrors.BillNotFound;

        var customer = await customers.GetByIdAsync(customerId, ct);
        var trouble = Trouble.Create(customerId, customer?.Code ?? "", customer?.CompanyName ?? "", user.StaffId, user.UserName ?? "",
            bill.Bill,
            Cut(string.IsNullOrWhiteSpace(r.Cnee) ? bill.ConsigneeName : r.Cnee, Trouble.NameMaxLength),
            Cut(string.IsNullOrWhiteSpace(r.Ct) ? bill.ConsigneeCountry : r.Ct, Trouble.CountryMaxLength),
            r.Type!.Trim(), r.Lv!, r.Desc!.Trim(), r.Req!.Trim(), r.Contact!.Trim(), VietnamTime.Now(clock));
        db.Troubles.Add(trouble);
        await db.SaveChangesAsync(ct);
        // Báo về hộp thư Việt An ở nền — sự cố đã lưu, xử lý ở trang /admin.
        jobs.EnqueueEmail($"email sự cố {trouble.Code}", TroubleRules.InboxEmail(trouble, customer, company));
        return TroubleRules.ToDto(trouble);
    }

    public async Task<Result<TroubleDto>> Handle(ReplyTroubleCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return FeedbackErrors.NotLoggedIn;
        if (c.Status is not (null or TroubleStatus.Done)) return TroubleErrors.Status;
        var text = c.Reply?.Trim() ?? "";
        if (text.Length is 0 or > TroubleMessage.MessageMaxLength) return TroubleErrors.Reply;
        if (Trouble.ParseCode(c.Id) is not { } id) return TroubleErrors.NotFound;

        var trouble = await Mine(customerId).Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (trouble is null) return TroubleErrors.NotFound;
        if (trouble.Status == TroubleStatus.Done) return TroubleErrors.Closed;

        trouble.CustomerReply(user.UserName ?? trouble.Requester, text, c.Status == TroubleStatus.Done, VietnamTime.Now(clock));
        await db.SaveChangesAsync(ct);
        return TroubleRules.ToDto(trouble);
    }

    private static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();
}

// ---------------- Quản trị Việt An ----------------

internal sealed record GetAdminTroublesQuery : IRequest<IReadOnlyList<AdminTroubleDto>>;

/// <summary>Việt An trả lời và / hoặc đổi trạng thái → thông báo riêng cho khách.</summary>
internal sealed record AdminReplyTroubleCommand(string Id, string? Reply, string? Status) : IRequest<Result<AdminTroubleDto>>;

internal sealed class AdminTroubleHandlers(IdentityDbContext db, ICurrentUser user, EmailCompany company, TimeProvider clock) :
    IRequestHandler<GetAdminTroublesQuery, IReadOnlyList<AdminTroubleDto>>,
    IRequestHandler<AdminReplyTroubleCommand, Result<AdminTroubleDto>>
{
    private const int ListLimit = 300;

    public async Task<IReadOnlyList<AdminTroubleDto>> Handle(GetAdminTroublesQuery q, CancellationToken ct)
    {
        var rows = await db.Troubles.AsNoTracking().Include(t => t.Messages)
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<AdminTroubleDto>> Handle(AdminReplyTroubleCommand c, CancellationToken ct)
    {
        if (c.Status is { } status && !TroubleStatus.All.Contains(status)) return TroubleErrors.Status;
        if ((c.Reply?.Trim().Length ?? 0) > TroubleMessage.MessageMaxLength) return TroubleErrors.Reply;
        if (Trouble.ParseCode(c.Id) is not { } id) return TroubleErrors.NotFound;
        var trouble = await db.Troubles.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (trouble is null) return TroubleErrors.NotFound;

        var now = VietnamTime.Now(clock);
        var before = trouble.Status;
        var author = "CS Việt An";
        if (!trouble.VietAnReply(author, c.Reply, c.Status, now)) return TroubleErrors.NothingToSend;
        db.Notifications.Add(TroubleRules.CustomerNotice(trouble, c.Reply, trouble.Status != before, user.UserName ?? author,
            company.Portal("/troubles"), now));
        await db.SaveChangesAsync(ct);
        return ToDto(trouble);
    }

    private static AdminTroubleDto ToDto(Trouble t) => new(
        TroubleRules.ToDto(t), t.CustomerCode, t.CompanyName, t.SenderUserName, t.StaffId is not null, TroubleRules.Time(t.UpdatedAt));
}
