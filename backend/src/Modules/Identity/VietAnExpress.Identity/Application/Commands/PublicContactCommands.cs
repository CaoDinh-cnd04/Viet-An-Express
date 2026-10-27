using FluentValidation;
using MediatR;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>Form "Gửi lời nhắn" ở trang chủ — khách vãng lai, chưa đăng nhập. Giới hạn khớp frontend (landing/schema.ts).</summary>
internal sealed record PublicContactCommand(string? Name, string? Phone, string? Email, string? Message) : IRequest<Result<string>>;

internal sealed class PublicContactCommandValidator : AbstractValidator<PublicContactCommand>
{
    public const int NameMaxLength = 100;
    public const int MessageMinLength = 10;
    public const int MessageMaxLength = 2000;
    /// <summary>9–15 ký tự: chữ số, +, khoảng trắng, dấu chấm, gạch nối (như frontend).</summary>
    public const string PhonePattern = @"^\+?[\d\s.-]{9,15}$";

    public PublicContactCommandValidator()
    {
        RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Nhập họ tên")
            .Must(v => (v?.Trim().Length ?? 0) <= NameMaxLength).WithMessage($"Họ tên tối đa {NameMaxLength} ký tự");
        RuleFor(x => x.Phone).Must(v => v is not null && System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), PhonePattern))
            .WithMessage("Số điện thoại không hợp lệ");
        RuleFor(x => x.Email).Must(v => string.IsNullOrWhiteSpace(v) || SupportRules.IsEmail(v)).WithMessage("Email không hợp lệ");
        RuleFor(x => x.Message).Must(v => (v?.Trim().Length ?? 0) is >= MessageMinLength and <= MessageMaxLength)
            .WithMessage($"Nội dung từ {MessageMinLength} đến {MessageMaxLength} ký tự");
    }
}

/// <summary>
/// Ghi lời nhắn vào dbo.TContact (KeyWord "ContactHome" như form website cũ, FormType "Portal - Liên hệ", CustID = 0) để phần mềm cũ / trang /admin xử lý,
/// rồi gửi email về hộp thư Việt An (bấm Trả lời là phản hồi khách nếu khách để lại email).
/// </summary>
internal sealed class PublicContactHandler(IdentityDbContext db, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock)
    : IRequestHandler<PublicContactCommand, Result<string>>
{
    public async Task<Result<string>> Handle(PublicContactCommand c, CancellationToken ct)
    {
        var email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim();
        // KeyWord "ContactHome", Title trống — giống form liên hệ trang chủ website cũ (màn CS p_TContactHome_Rows).
        var contact = Trouble.Create(TroubleKind.Contact, customerId: 0, companyName: "", staffId: null,
            bill: null, consignee: null, country: null, type: TroubleKind.ContactKeyWord, subject: null, priority: "mid",
            description: c.Message!.Trim(), requester: c.Name!.Trim(), contact: c.Phone!.Trim(), email: email, now: VietnamTime.Now(clock));
        db.Troubles.Add(contact);
        await db.SaveChangesAsync(ct);
        jobs.EnqueueEmail($"email {contact.Code}", ContactEmail(contact, company));
        return "Đã nhận lời nhắn — Việt An sẽ liên hệ lại với bạn sớm";
    }

    /// <summary>Email báo lời nhắn mới về hộp thư Việt An. Hàm thuần — có test.</summary>
    public static EmailMessage ContactEmail(Trouble t, EmailCompany company)
    {
        var replyTo = SupportRules.ReplyTo(t.Email, null);
        var content = new EmailContent(
            Title: $"Lời nhắn từ trang chủ: {t.Requester}",
            Badge: TroubleKind.Label(t.Kind),
            Meta: $"{t.Code} · {t.CreatedAt:dd/MM/yyyy HH:mm}",
            Sections:
            [
                new EmailSection("Nội dung", t.Description),
                new EmailSection("Người gửi", Fields:
                [
                    new EmailField("Họ tên", t.Requester),
                    new EmailField("Điện thoại", t.Contact),
                    new EmailField("Email", t.Email ?? "—")
                ])
            ],
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Xử lý trong trang quản trị", admin) : null,
            FooterNote: SupportRules.ReplyNote(replyTo, t.Contact));
        return new EmailMessage(
            $"[Liên hệ trang chủ] {t.Code} — {t.Requester} ({t.Contact})",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            HtmlBody: EmailTemplate.Html(content, company));
    }
}
