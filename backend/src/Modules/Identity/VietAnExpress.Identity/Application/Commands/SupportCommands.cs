using System.Net.Mail;
using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>Tệp khách đính kèm yêu cầu hỗ trợ (đã đọc vào bộ nhớ ở controller).</summary>
internal sealed record SupportAttachment(string FileName, string ContentType, byte[] Data);

/// <summary>
/// Yêu cầu hỗ trợ ở trang Trợ giúp & Góp ý: lưu vào dbo.GopY (hiện ở danh sách góp ý trang quản trị /admin, ảnh đính kèm lưu
/// dbo.GopY_HinhAnh) và gửi email về hộp thư nhận của Việt An (Email:InboxAddress) như trước.
/// </summary>
internal sealed record SendSupportRequestCommand(string? Category, string? Subject, string? Message, string? Contact, SupportAttachment? Attachment)
    : IRequest<Result<bool>>;

internal static class SupportErrors
{
    public static readonly Error Category = Error.Validation("SUPPORT_CATEGORY", "Chọn chủ đề");
    public static readonly Error Subject = Error.Validation("SUPPORT_SUBJECT", $"Tiêu đề từ {SupportRules.SubjectMin} đến {SupportRules.SubjectMax} ký tự");
    public static readonly Error Message = Error.Validation("SUPPORT_MESSAGE", $"Nội dung từ {SupportRules.MessageMin} đến {SupportRules.MessageMax} ký tự");
    public static readonly Error Contact = Error.Validation("SUPPORT_CONTACT", "Nhập SĐT hoặc email để Việt An liên hệ lại");
    public static readonly Error AttachmentTooLarge = Error.Validation("SUPPORT_ATTACHMENT_TOO_LARGE", "Tệp đính kèm tối đa 10 MB");
    public static readonly Error AttachmentType = Error.Validation("SUPPORT_ATTACHMENT_TYPE", "Chỉ nhận ảnh, PDF, Excel, CSV hoặc Word");
    public static readonly Error EmailNotConfigured = Error.BusinessRule("EMAIL_NOT_CONFIGURED",
        "Chức năng gửi yêu cầu đang tạm ngưng — vui lòng gọi hotline hoặc gửi email cho Việt An");
}

/// <summary>Quy tắc yêu cầu hỗ trợ + nội dung email — hàm thuần, có test.</summary>
internal static class SupportRules
{
    public const int CategoryMax = 100;
    public const int SubjectMin = 5;
    public const int SubjectMax = 200;
    public const int MessageMin = 20;
    public const int MessageMax = 4000;
    public const int ContactMin = 6;
    public const int ContactMax = 200;
    public const int AttachmentMaxBytes = 10 * 1024 * 1024;

    /// <summary>Đuôi tệp nhận (khớp <c>accept</c> ở frontend).</summary>
    public static readonly IReadOnlySet<string> AttachmentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".pdf", ".xlsx", ".xls", ".csv", ".doc", ".docx" };

    public static Error? Validate(SendSupportRequestCommand c)
    {
        if ((c.Category?.Trim().Length ?? 0) is 0 or > CategoryMax) return SupportErrors.Category;
        if ((c.Subject?.Trim().Length ?? 0) is < SubjectMin or > SubjectMax) return SupportErrors.Subject;
        if ((c.Message?.Trim().Length ?? 0) is < MessageMin or > MessageMax) return SupportErrors.Message;
        if ((c.Contact?.Trim().Length ?? 0) is < ContactMin or > ContactMax) return SupportErrors.Contact;
        if (c.Attachment is { } a)
        {
            if (a.Data.Length is 0 or > AttachmentMaxBytes) return SupportErrors.AttachmentTooLarge;
            if (!AttachmentExtensions.Contains(Path.GetExtension(a.FileName))) return SupportErrors.AttachmentType;
        }
        return null;
    }

    /// <summary>Địa chỉ trả lời: SĐT/email khách nhập là email hợp lệ thì dùng, không thì email hồ sơ khách.</summary>
    public static string? ReplyTo(string? contact, string? profileEmail) =>
        IsEmail(contact) ? contact!.Trim() : IsEmail(profileEmail) ? profileEmail!.Trim() : null;

    public static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value.Trim(), out var a) && a.Address == value.Trim();

    /// <summary>Bảng "Thông tin khách" chung cho email hỗ trợ và góp ý.</summary>
    public static IReadOnlyList<EmailField> CustomerFields(CustomerSummary? customer, string? userName, bool isStaff, string? contact) =>
    [
        new("Công ty", customer?.CompanyName ?? "(không rõ)"),
        new("Mã khách", customer?.Code ?? "—"),
        new("Người gửi", $"{(isStaff ? "Tài khoản con" : "Tài khoản chính")} {userName}"),
        new("Liên hệ lại", string.IsNullOrWhiteSpace(contact) ? "—" : contact.Trim()),
        new("Người liên hệ (hồ sơ)", customer?.ContactName ?? "—"),
        new("Điện thoại (hồ sơ)", customer?.Phone ?? "—"),
        new("Email (hồ sơ)", customer?.Email ?? "—")
    ];

    /// <summary>Câu cuối email gửi về hộp thư Việt An: trả lời thẳng cho khách được không.</summary>
    public static string ReplyNote(string? replyTo, string? contact) => replyTo is not null
        ? $"Bấm Trả lời để phản hồi khách qua {replyTo}."
        : $"Khách không để lại email — liên hệ lại qua {(string.IsNullOrWhiteSpace(contact) ? "thông tin trong hồ sơ" : contact.Trim())}.";

    /// <summary>Tệp đính kèm là ảnh hợp lệ cho dbo.GopY_HinhAnh (JPG / PNG / WEBP / GIF ≤ 5 MB) — tệp khác chỉ gửi qua email.</summary>
    public static bool IsStorableImage(SupportAttachment a) =>
        Feedback.ImageTypes.Contains(a.ContentType) && a.Data.Length is > 0 and <= Feedback.ImageMaxBytes && FeedbackRules.LooksLikeImage(a.Data);

    /// <summary>
    /// Nội dung lưu dbo.GopY: "[Hỗ trợ · chủ đề] tiêu đề" + nội dung; tệp không lưu được (PDF, Excel, ảnh quá lớn…) ghi tên để admin tìm trong email.
    /// Cắt theo độ dài tối đa của góp ý.
    /// </summary>
    public static string FeedbackMessage(SendSupportRequestCommand c)
    {
        var text = $"[Hỗ trợ · {c.Category!.Trim()}] {c.Subject!.Trim()}\n\n{c.Message!.Trim()}";
        if (c.Attachment is { } a && !IsStorableImage(a)) text += $"\n\nĐính kèm (xem trong email): {a.FileName}";
        return text.Length <= Feedback.MessageMaxLength ? text : text[..Feedback.MessageMaxLength];
    }

    public static EmailMessage Email(SendSupportRequestCommand c, CustomerSummary? customer, string? userName, bool isStaff, EmailCompany company)
    {
        var replyTo = ReplyTo(c.Contact, customer?.Email);
        var content = new EmailContent(
            Title: c.Subject!.Trim(),
            Badge: $"Yêu cầu hỗ trợ · {c.Category!.Trim()}",
            Meta: $"Từ {customer?.CompanyName ?? "khách hàng"} ({customer?.Code ?? "?"}) qua portal Việt An Express",
            Sections:
            [
                new EmailSection("Nội dung", c.Message!.Trim()),
                new EmailSection("Thông tin khách", Fields:
                [
                    .. CustomerFields(customer, userName, isStaff, c.Contact),
                    .. c.Attachment is { } a ? [new EmailField("Đính kèm", a.FileName)] : Array.Empty<EmailField>()
                ])
            ],
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Xem trong trang quản trị", admin) : null,
            FooterNote: ReplyNote(replyTo, c.Contact));
        return new EmailMessage(
            $"[Hỗ trợ portal] {c.Category!.Trim()}: {c.Subject!.Trim()} — {customer?.Code ?? "?"}",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            Attachments: c.Attachment is { } file ? [new EmailAttachment(file.FileName, file.ContentType, file.Data)] : null,
            HtmlBody: EmailTemplate.Html(content, company));
    }
}

internal sealed class SupportHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IEmailSender email, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock) :
    IRequestHandler<SendSupportRequestCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SendSupportRequestCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return FeedbackErrors.NotLoggedIn;
        if (SupportRules.Validate(c) is { } error) return error;

        // Lưu vào góp ý trước: email lỗi / chưa cấu hình thì yêu cầu vẫn nằm ở trang quản trị, không bị mất.
        var customer = await customers.GetByIdAsync(customerId, ct);
        var images = c.Attachment is { } a && SupportRules.IsStorableImage(a)
            ? [new FeedbackImage(FeedbackRules.SafeFileName(a.FileName), a.ContentType.ToLowerInvariant(), a.Data)]
            : Array.Empty<FeedbackImage>();
        db.Feedbacks.Add(Feedback.Create(customerId, customer?.Code ?? "", customer?.CompanyName ?? "", user.StaffId, user.UserName ?? "",
            SupportRules.FeedbackMessage(c), c.Contact?.Trim(), null, images, VietnamTime.Now(clock)));
        await db.SaveChangesAsync(ct);

        if (email.IsConfigured)
            jobs.EnqueueEmail("email yêu cầu hỗ trợ", SupportRules.Email(c, customer, user.UserName, user.StaffId is not null, company));
        return true;
    }
}
