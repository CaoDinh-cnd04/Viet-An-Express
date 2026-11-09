using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.Shared;

/// <summary>
/// Email mã OTP đăng nhập portal (nhân viên Việt An + khách), kiểu đơn giản như email mã xác thực của GitHub: logo + tên Việt An Express
/// căn trái (logo nhúng cid:), tiêu đề, khung viền mảnh chứa mã chữ to giãn cách, thời hạn, lời nhắc không chia sẻ mã; dưới khung là lý do nhận email
/// và chân thông tin công ty. Kèm bản chữ thuần.
/// Bảng + style inline để Gmail / Outlook / điện thoại hiển thị giống nhau. Hàm thuần (trừ đọc logo nhúng) — có test.
/// </summary>
internal static class OtpEmail
{
    /// <summary>Content-ID của logo trong email (<c>&lt;img src="cid:va-logo"&gt;</c>).</summary>
    public const string LogoCid = "va-logo";

    private const string Brand = "#0b4d39";
    private const string Text = "#1f2933";
    private const string Muted = "#6b7280";
    private const string Border = "#e5e7eb";
    private const string Font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    /// <param name="securityNote">Câu dặn khi không phải người nhận đang đăng nhập (khách / nhân viên khác nhau).</param>
    public static EmailMessage Build(string to, string name, string code, TimeSpan lifetime, string securityNote, EmailCompany company)
    {
        var minutes = lifetime.TotalMinutes.ToString(CultureInfo.InvariantCulture);
        var logo = Logo();
        return new EmailMessage(
            $"Mã đăng nhập portal Việt An: {code}",
            PlainText(name, code, minutes, securityNote, company),
            To: [to],
            Attachments: logo is null ? null : [new EmailAttachment("viet-an-express.png", "image/png", logo, LogoCid)],
            HtmlBody: Html(name, code, minutes, securityNote, company, logo is not null));
    }

    public static string Html(string name, string code, string minutes, string securityNote, EmailCompany company, bool withLogo)
    {
        var h = new StringBuilder();
        h.Append($"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light">
            <title>Mã đăng nhập portal Việt An</title>
            </head>
            <body style="margin:0;padding:0;background:#ffffff;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">Mã đăng nhập của bạn là {E(code)} — hiệu lực {E(minutes)} phút.</div>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#ffffff;">
            <tr><td align="center" style="padding:28px 16px;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;font-family:{Font};color:{Text};">
            <tr><td style="padding:0 0 22px;">
            <table role="presentation" cellspacing="0" cellpadding="0" border="0"><tr>
            """);
        if (withLogo)
            h.Append($"""<td valign="middle" style="padding-right:10px;"><img src="cid:{LogoCid}" alt="" width="36" height="36" style="display:block;width:36px;height:36px;border:0;"></td>""");
        h.Append($"""
            <td valign="middle" style="font-size:17px;font-weight:700;color:{Brand};">{E(company.Name)}</td>
            </tr></table>
            </td></tr>
            <tr><td style="padding:0 0 18px;font-size:22px;line-height:1.35;color:{Text};">Mã xác nhận đăng nhập, <strong>{E(name)}</strong></td></tr>
            <tr><td style="padding:20px 24px;border:1px solid {Border};border-radius:6px;font-size:14px;line-height:1.6;color:{Text};">
            <p style="margin:0 0 14px;">Đây là mã đăng nhập portal Việt An Express dành cho bạn:</p>
            <p style="margin:0 0 16px;text-align:center;font-size:28px;letter-spacing:6px;color:{Text};">{E(code)}</p>
            <p style="margin:0 0 12px;">Mã này có hiệu lực trong <strong>{E(minutes)} phút</strong> và chỉ có thể sử dụng một lần.</p>
            <p style="margin:0 0 12px;"><strong>Vui lòng không chia sẻ mã này với bất kỳ ai:</strong> Việt An không bao giờ yêu cầu mã này qua điện thoại hoặc email.</p>
            <p style="margin:0;">Cảm ơn,<br>{E(company.Name)}</p>
            </td></tr>
            <tr><td style="padding:18px 0 0;font-size:13px;line-height:1.6;color:{Muted};">
            Bạn nhận được email này vì có yêu cầu đăng nhập portal Việt An Express bằng tài khoản của bạn. {E(securityNote)}
            </td></tr>
            <tr><td style="padding:18px 0 0;"><div style="border-top:1px solid {Border};"></div></td></tr>
            <tr><td style="padding:12px 0 0;font-size:12px;line-height:1.6;color:{Muted};">
            """);
        var footer = new[]
        {
            E(company.Name), company.Address is { } address ? E(address) : null,
            company.Hotline is { } hotline ? $"Hotline: {E(hotline)}" : null, company.Website is { } web ? E(web) : null
        }.OfType<string>();
        h.Append(string.Join(" · ", footer));
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

    public static string PlainText(string name, string code, string minutes, string securityNote, EmailCompany company)
    {
        var t = new StringBuilder();
        t.Append("Mã xác nhận đăng nhập, ").Append(name).Append("\n\n");
        t.Append("Đây là mã đăng nhập portal Việt An Express dành cho bạn: ").Append(code).Append('\n');
        t.Append("Mã này có hiệu lực trong ").Append(minutes).Append(" phút và chỉ có thể sử dụng một lần.\n\n");
        t.Append("Vui lòng không chia sẻ mã này với bất kỳ ai: Việt An không bao giờ yêu cầu mã này qua điện thoại hoặc email.\n\n");
        t.Append("Cảm ơn,\n").Append(company.Name).Append("\n\n");
        t.Append("Bạn nhận được email này vì có yêu cầu đăng nhập portal Việt An Express bằng tài khoản của bạn. ").Append(securityNote).Append("\n\n—\n");
        t.Append(company.Name).Append('\n');
        if (company.Address is { } address) t.Append(address).Append('\n');
        if (company.Hotline is { } hotline) t.Append("Hotline: ").Append(hotline).Append('\n');
        if (company.Website is { } website) t.Append(website).Append('\n');
        return t.ToString();
    }

    private static byte[]? logo;

    /// <summary>Logo PNG nhúng trong assembly (Shared/Assets/email-logo.png); không đọc được → email không có logo.</summary>
    private static byte[]? Logo()
    {
        if (logo is not null) return logo;
        using var stream = typeof(OtpEmail).Assembly.GetManifestResourceStream("email-logo.png");
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return logo = ms.ToArray();
    }

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static string E(string value) => Encoder.Encode(value);
}
