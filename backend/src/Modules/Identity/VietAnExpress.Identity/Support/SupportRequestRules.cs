using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

/// <summary>Quy tắc yêu cầu hỗ trợ &amp; sự cố — hàm thuần, có test.</summary>
internal static class SupportRequestRules
{
    /// <summary>
    /// Nội dung góp ý — đúng 4 mục ô "Chọn nội dung góp ý" của form "Yêu cầu" (Bill Online cũ), lưu TContact.Title (KeyWord = "Feedback") như form cũ.
    /// Khách không chọn (client cũ) → "Khác".
    /// </summary>
    public static readonly IReadOnlyList<string> FeedbackTopics = ["Chỉnh sửa phần mềm", "Cập nhật thông tin", "Góp ý nâng cấp", "Khác"];

    public const string FeedbackType = "Khác";

    /// <summary>Hỗ trợ và sự cố là 1 loại (lưu "Portal - Hỗ trợ"); su_co của frontend cũ cũng gộp vào đây.</summary>
    public static string KindOf(NewSupportRequest r) =>
        r.Kind?.Trim() is { Length: > 0 } k && k != SupportRequestKind.Incident ? k : SupportRequestKind.Support;

    public static Error? Validate(NewSupportRequest r, SupportRequestFile? file = null)
    {
        var kind = KindOf(r);
        if (!SupportRequestKind.All.Contains(kind) || kind == SupportRequestKind.Incident) return SupportRequestErrors.Kind;
        var support = kind == SupportRequestKind.Support;

        if ((r.Bill?.Trim().Length ?? 0) > SupportRequest.BillMaxLength) return SupportRequestErrors.Bill;
        if (support && (r.Type?.Trim().Length ?? 0) is 0 or > SupportRequest.TypeMaxLength) return SupportRequestErrors.Type;
        if (!support && r.Type?.Trim() is { Length: > 0 } topic && !FeedbackTopics.Contains(topic)) return SupportRequestErrors.FeedbackTopic;
        if ((r.Subject?.Trim().Length ?? 0) > SupportRequest.SubjectMaxLength) return SupportRequestErrors.Subject;
        if (r.Lv is not null && !SupportRequestPriority.All.Contains(r.Lv)) return SupportRequestErrors.Priority;
        if ((r.Desc?.Trim().Length ?? 0) is < SupportRequest.DescriptionMinLength or > SupportRequest.DescriptionMaxLength) return SupportRequestErrors.Description;
        if ((r.Req?.Trim().Length ?? 0) > SupportRequest.RequesterMaxLength) return SupportRequestErrors.Requester;
        var contact = r.Contact?.Trim().Length ?? 0;
        if ((support && contact == 0) || contact > SupportRequest.ContactMaxLength) return SupportRequestErrors.Contact;
        if (file is not null)
        {
            if (file.Data.Length is 0 or > SupportRequest.FileMaxBytes) return SupportRequestErrors.FileTooLarge;
            if (!SupportRequest.FileExtensions.Contains(Path.GetExtension(file.Name))) return SupportRequestErrors.FileType;
        }
        return null;
    }

    public static string Time(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static DateTime ParseTime(string value) =>
        DateTime.TryParseExact(value, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : DateTime.MinValue;

    public static SupportRequestDto ToDto(SupportRequest t)
    {
        var (desc, thread) = SupportThread.Parse(t.Description);
        return new(t.Code, t.Kind, t.Bill ?? "", t.ConsigneeName ?? "", t.Country ?? "", t.Topic, t.Subject, t.Priority ?? "low",
            desc, t.Requester, t.Contact, Time(t.CreatedAt), t.Status, Time(t.UpdatedAt), thread);
    }

    /// <summary>Dòng ở trang quản trị: yêu cầu kèm mã / tên khách gửi và có phải tài khoản con không.</summary>
    public static AdminSupportRequestDto ToAdminDto(SupportRequest t, string? customerCode) =>
        new(ToDto(t), customerCode ?? "", t.CompanyName, t.StaffId is not null, Email: t.Email);

    /// <summary>Tên hiển thị: "Sự cố TRB-12" / "Yêu cầu hỗ trợ TRB-13" / "Góp ý TRB-14".</summary>
    public static string Title(SupportRequest t) => $"{SupportRequestKind.Label(t.Kind)} {t.Code}";

    /// <summary>Dòng đầu nội dung thông báo / email: vận đơn (sự cố) hoặc tiêu đề (hỗ trợ) kèm loại / chủ đề.</summary>
    public static string Summary(SupportRequest t) =>
        t.Bill is { Length: > 0 } bill ? $"Mã vận đơn: {bill} · {t.Topic}"
        : t.Subject is { Length: > 0 } subject ? $"{subject} · {t.Topic}"
        : t.Topic;

    /// <summary>Email báo yêu cầu mới về hộp thư Việt An — kèm tệp khách đính kèm (tệp không lưu database).</summary>
    public static EmailMessage InboxEmail(SupportRequest t, CustomerSummary? customer, string? userName, SupportRequestFile? file, EmailCompany company)
    {
        var replyTo = SupportRules.ReplyTo(t.Contact, customer?.Email);
        var incident = !string.IsNullOrEmpty(t.Bill); // có vận đơn → kèm mức độ + thông tin vận đơn
        var heading = t.Subject is { Length: > 0 } subject ? subject : t.Topic;
        var content = new EmailContent(
            Title: $"{Title(t)}: {heading}",
            Badge: incident ? t.Priority switch { "high" => "Rất gấp", "mid" => "Gấp", _ => "Thường" } : $"{SupportRequestKind.Label(t.Kind)} · {t.Topic}",
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
            $"[{SupportRequestKind.Label(t.Kind)} portal] {t.Code} — {heading}{(incident ? $" — {t.Bill}" : "")} ({customer?.Code ?? "?"})",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            Attachments: file is null ? null : [new EmailAttachment(file.Name, file.ContentType, file.Data)],
            HtmlBody: EmailTemplate.Html(content, company));
    }

    /// <summary>Email báo hộp thư Việt An: khách vừa trả lời yêu cầu đang chờ.</summary>
    public static EmailMessage CustomerReplyEmail(SupportRequest t, string text, CustomerSummary? customer, string? userName, EmailCompany company)
    {
        var replyTo = SupportRules.ReplyTo(t.Contact, customer?.Email);
        var content = new EmailContent(
            Title: $"Khách phản hồi {Title(t)}",
            Badge: "Khách phản hồi",
            Meta: $"{(string.IsNullOrEmpty(t.Bill) ? "" : $"Vận đơn {t.Bill} · ")}{t.CompanyName} ({customer?.Code ?? "?"}) · {Time(t.UpdatedAt)}",
            Sections:
            [
                new EmailSection("Khách trả lời", SupportThread.Clean(text)),
                new EmailSection("Thông tin khách", Fields: SupportRules.CustomerFields(customer, userName, t.StaffId is not null, t.Contact))
            ],
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Xử lý trong trang quản trị", admin) : null,
            FooterNote: SupportRules.ReplyNote(replyTo, t.Contact));
        return new EmailMessage(
            $"[Khách phản hồi portal] {t.Code} — {t.Topic} ({customer?.Code ?? "?"})",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            HtmlBody: EmailTemplate.Html(content, company));
    }

    public static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();
}
