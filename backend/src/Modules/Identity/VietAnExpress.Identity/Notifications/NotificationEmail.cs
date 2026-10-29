using System.Globalization;
using System.Net.Mail;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Email thông báo gửi khách: giới hạn người nhận (Gmail), lọc địa chỉ, ảnh nhúng và nội dung email — hàm thuần, có test.</summary>
internal static class NotificationEmail
{
    /// <summary>Gmail cá nhân giới hạn ~500 người nhận / ngày — chừa lại cho email góp ý / hỗ trợ.</summary>
    public const int MaxEmailRecipients = 450;

    /// <summary>Số người nhận (BCC) mỗi email — Gmail giới hạn 100 / email.</summary>
    public const int EmailBatchSize = 50;

    /// <summary>Số địa chỉ tối đa khi gửi thử.</summary>
    public const int MaxTestRecipients = 5;

    /// <summary>Email nhận thử ("a@x.vn, b@y.vn") → danh sách hợp lệ; null nếu trống, có địa chỉ sai hoặc quá <see cref="MaxTestRecipients"/>.</summary>
    public static IReadOnlyList<string>? TestRecipients(string? value)
    {
        var parts = (value ?? "").Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (emails, _) = Recipients(parts, int.MaxValue);
        return parts.Length == 0 || emails.Count != parts.Distinct(StringComparer.OrdinalIgnoreCase).Count() || emails.Count > MaxTestRecipients ? null : emails;
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
        foreach (var name in n.Items.OrderBy(i => i.SortOrder).SelectMany(NotificationRules.ImageNames).Distinct())
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
                    Images: NotificationRules.ImageNames(i).Where(embedded.Contains).Select(name => "cid:" + name).ToList(),
                    Link: i.Link is { } link ? new EmailLink("Xem chi tiết", link)
                        : i.Detail is not null && onPortal is not null ? new EmailLink("Xem chi tiết trên portal", onPortal) : null,
                    Html: RichText.IsRich(i.Body) ? i.Body : null))
                .ToList();
        var content = new EmailContent(
            Title: n.Title,
            Badge: n.Important ? "Thông báo quan trọng" : null,
            Meta: $"Việt An Express · {NotificationRules.Time(n.PublishAt)}",
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
