using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.Configuration;

namespace VietAnExpress.Identity.Application;

/// <summary>Thông tin công ty ở chân email (mục cấu hình <c>Company</c>).</summary>
internal sealed record EmailCompany(string Name, string? Address, string? Hotline, string? Website, string? PortalUrl)
{
    public static EmailCompany From(IConfiguration c) => new(
        c["Company:Name"] is { Length: > 0 } name ? name : "Việt An Express",
        Clean(c["Company:Address"]), Clean(c["Company:Hotline"]), Clean(c["Company:Website"]), Clean(c["Company:PortalUrl"])?.TrimEnd('/'));

    /// <summary>Đường dẫn trong portal (vd "/notifications"); chưa cấu hình PortalUrl → null.</summary>
    public string? Portal(string path) => PortalUrl is null ? null : PortalUrl + path;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

internal sealed record EmailField(string Label, string Value);

/// <summary>1 khối nội dung: tiêu đề nhỏ (không bắt buộc), đoạn chữ nhiều dòng và / hoặc bảng nhãn – giá trị.</summary>
internal sealed record EmailSection(string? Heading, string? Text = null, IReadOnlyList<EmailField>? Fields = null);

internal sealed record EmailLink(string Label, string Url);

/// <summary>Nội dung 1 email theo khung chung của Việt An.</summary>
internal sealed record EmailContent(
    string Title,
    IReadOnlyList<EmailSection> Sections,
    string? Badge = null,
    string? Meta = null,
    EmailLink? Button = null,
    EmailLink? SecondaryLink = null,
    string? FooterNote = null)
{
    /// <summary>Dòng xem trước hiện cạnh tiêu đề trong danh sách thư.</summary>
    public string Preheader => Sections.Select(s => s.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) is { } t
        ? (t.ReplaceLineEndings(" ").Trim() is var one && one.Length > 120 ? one[..120] + "…" : one)
        : Title;
}

/// <summary>
/// Khung email HTML của Việt An (bảng + style inline để Gmail / Outlook / điện thoại hiển thị giống nhau) và bản chữ thuần đi kèm.
/// Mọi chữ đều được mã hóa HTML; link chỉ nhận http / https. Hàm thuần — có test.
/// </summary>
internal static class EmailTemplate
{
    private const string Brand = "#2e8b3e";
    private const string BrandStrong = "#1a6128";
    private const string Text = "#1f2933";
    private const string Muted = "#6b7280";
    private const string Border = "#e5e7eb";
    private const string Font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    public static string Html(EmailContent c, EmailCompany company)
    {
        var h = new StringBuilder();
        h.Append($"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light">
            <title>{E(c.Title)}</title>
            </head>
            <body style="margin:0;padding:0;background:#f2f5f3;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{E(c.Preheader)}</div>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#f2f5f3;">
            <tr><td align="center" style="padding:24px 12px;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;background:#ffffff;border:1px solid {Border};border-radius:12px;overflow:hidden;font-family:{Font};">
            <tr><td style="background:{Brand};padding:18px 28px;">
            <span style="font-size:18px;font-weight:700;color:#ffffff;letter-spacing:.2px;">Việt An Express</span>
            </td></tr>
            <tr><td style="padding:28px 28px 8px;">
            """);

        if (c.Badge is { Length: > 0 } badge)
            h.Append($"""<div style="margin:0 0 12px;"><span style="display:inline-block;padding:4px 12px;border-radius:999px;background:#fff4e5;color:#9a5b00;font-size:12px;font-weight:600;">{E(badge)}</span></div>""");
        h.Append($"""<h1 style="margin:0;font-size:22px;line-height:1.35;font-weight:700;color:{Text};">{E(c.Title)}</h1>""");
        if (c.Meta is { Length: > 0 } meta)
            h.Append($"""<p style="margin:6px 0 0;font-size:13px;color:{Muted};">{E(meta)}</p>""");
        h.Append("</td></tr>");

        foreach (var s in c.Sections)
        {
            h.Append("""<tr><td style="padding:16px 28px 0;">""");
            if (s.Heading is { Length: > 0 } heading)
                h.Append($"""<h2 style="margin:0 0 8px;font-size:14px;font-weight:700;color:{BrandStrong};">{E(heading)}</h2>""");
            if (s.Text is { Length: > 0 } text) h.Append(Paragraphs(text));
            if (s.Fields is { Count: > 0 } fields)
            {
                h.Append($"""<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;font-size:14px;">""");
                foreach (var f in fields)
                    h.Append($"""<tr><td valign="top" style="width:38%;padding:7px 12px 7px 0;border-bottom:1px solid {Border};color:{Muted};">{E(f.Label)}</td><td valign="top" style="padding:7px 0;border-bottom:1px solid {Border};color:{Text};">{Lines(f.Value)}</td></tr>""");
                h.Append("</table>");
            }
            h.Append("</td></tr>");
        }

        if (WebUrl(c.Button?.Url) is { } buttonUrl)
            h.Append($"""
                <tr><td style="padding:24px 28px 0;">
                <table role="presentation" cellspacing="0" cellpadding="0" border="0"><tr>
                <td bgcolor="{Brand}" style="border-radius:8px;">
                <a href="{E(buttonUrl)}" target="_blank" style="display:inline-block;padding:12px 26px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;">{E(c.Button!.Label)} &rarr;</a>
                </td></tr></table>
                </td></tr>
                """);
        if (WebUrl(c.SecondaryLink?.Url) is { } secondaryUrl)
            h.Append($"""<tr><td style="padding:14px 28px 0;font-size:13px;"><a href="{E(secondaryUrl)}" target="_blank" style="color:{BrandStrong};">{E(c.SecondaryLink!.Label)}</a></td></tr>""");

        h.Append($"""
            <tr><td style="padding:28px 28px 0;"><div style="border-top:1px solid {Border};"></div></td></tr>
            <tr><td style="padding:16px 28px 24px;font-size:12px;line-height:1.6;color:{Muted};">
            <strong style="color:{Text};">{E(company.Name)}</strong><br>
            """);
        if (company.Address is { } address) h.Append($"{E(address)}<br>");
        var contacts = new[] { company.Hotline is { } hotline ? $"Hotline: {E(hotline)}" : null, company.Website is { } web ? E(web) : null }.OfType<string>();
        if (contacts.Any()) h.Append(string.Join(" · ", contacts)).Append("<br>");
        if (c.FooterNote is { Length: > 0 } note) h.Append($"""<span style="display:inline-block;margin-top:8px;">{E(note)}</span>""");
        h.Append("""
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);
        return h.ToString();
    }

    /// <summary>Bản chữ thuần cùng nội dung (trình đọc thư không hiện HTML, xem trước trên đồng hồ…).</summary>
    public static string PlainText(EmailContent c, EmailCompany company)
    {
        var t = new StringBuilder();
        if (c.Badge is { Length: > 0 } badge) t.Append('[').Append(badge).Append("]\n");
        t.Append(c.Title).Append('\n');
        if (c.Meta is { Length: > 0 } meta) t.Append(meta).Append('\n');
        foreach (var s in c.Sections)
        {
            t.Append('\n');
            if (s.Heading is { Length: > 0 } heading) t.Append(heading).Append('\n');
            if (s.Text is { Length: > 0 } text) t.Append(text.Trim()).Append('\n');
            foreach (var f in s.Fields ?? []) t.Append("- ").Append(f.Label).Append(": ").Append(f.Value).Append('\n');
        }
        if (WebUrl(c.Button?.Url) is { } buttonUrl) t.Append('\n').Append(c.Button!.Label).Append(": ").Append(buttonUrl).Append('\n');
        if (WebUrl(c.SecondaryLink?.Url) is { } secondaryUrl) t.Append(c.SecondaryLink!.Label).Append(": ").Append(secondaryUrl).Append('\n');
        t.Append("\n—\n").Append(company.Name).Append('\n');
        if (company.Address is { } address) t.Append(address).Append('\n');
        if (company.Hotline is { } hotline) t.Append("Hotline: ").Append(hotline).Append('\n');
        if (company.Website is { } website) t.Append(website).Append('\n');
        if (c.FooterNote is { Length: > 0 } note) t.Append(note).Append('\n');
        return t.ToString();
    }

    /// <summary>Đoạn văn: dòng trống tách đoạn, xuống dòng giữ nguyên.</summary>
    private static string Paragraphs(string text) =>
        string.Concat(text.ReplaceLineEndings("\n").Trim()
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => $"""<p style="margin:0 0 14px;font-size:15px;line-height:1.65;color:{Text};">{Lines(p)}</p>"""));

    private static string Lines(string value) => string.Join("<br>", value.ReplaceLineEndings("\n").Split('\n').Select(E));

    /// <summary>Giữ nguyên chữ tiếng Việt (không đổi sang &amp;#225;…), chỉ mã hóa ký tự HTML nguy hiểm.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static string E(string value) => Encoder.Encode(value);

    private static string? WebUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? url!.Trim() : null;
}
