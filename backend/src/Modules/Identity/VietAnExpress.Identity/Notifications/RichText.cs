using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VietAnExpress.Identity.Notifications;

/// <summary>
/// Nội dung có định dạng của thông báo (cỡ chữ, in đậm / nghiêng / gạch chân, màu chữ) do trình soạn ở /admin tạo.
/// Chỉ giữ thẻ và kiểu chữ trong danh sách cho phép, mọi thuộc tính khác (onclick, href, class…) bị bỏ, thẻ được đóng đủ.
/// Cùng quy tắc với frontend <c>features/notifications/lib/richText.ts</c>. Hàm thuần — có test.
/// </summary>
internal static partial class RichText
{
    private static readonly HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase) { "p", "div", "br", "b", "strong", "i", "em", "u", "span", "ul", "ol", "li" };
    /// <summary>Bỏ cả nội dung bên trong.</summary>
    private static readonly HashSet<string> Dropped = new(StringComparer.OrdinalIgnoreCase) { "script", "style", "iframe", "object", "embed", "template", "noscript", "head", "title", "svg", "math" };

    /// <summary>Có thẻ định dạng → nội dung HTML; không có → chữ thường (thông báo cũ / hệ thống).</summary>
    public static bool IsRich(string? value) => value is not null && RichTag().IsMatch(value);

    /// <summary>HTML → chỉ còn thẻ / kiểu chữ cho phép, đóng đủ thẻ.</summary>
    public static string Sanitize(string html)
    {
        var output = new StringBuilder(html.Length);
        var open = new List<string>();
        var skipUntil = (string?)null;
        foreach (Match m in Token().Matches(html))
        {
            var token = m.Value;
            if (skipUntil is not null)
            {
                if (m.Groups["close"].Success && m.Groups["name"].Value.Equals(skipUntil, StringComparison.OrdinalIgnoreCase)) skipUntil = null;
                continue;
            }
            if (token.StartsWith("<!--", StringComparison.Ordinal)) continue;
            if (!m.Groups["name"].Success)
            {
                output.Append(EscapeText(token));
                continue;
            }
            var name = m.Groups["name"].Value.ToLowerInvariant();
            if (m.Groups["close"].Success)
            {
                var at = open.LastIndexOf(name);
                if (at < 0) continue;
                for (var k = open.Count - 1; k >= at; k--) output.Append("</").Append(open[k]).Append('>');
                open.RemoveRange(at, open.Count - at);
                continue;
            }
            if (Dropped.Contains(name))
            {
                if (!m.Groups["self"].Success) skipUntil = name;
                continue;
            }
            if (!Tags.Contains(name)) continue;
            if (name == "br")
            {
                output.Append("<br>");
                continue;
            }
            var style = Style(m.Groups["attrs"].Value);
            output.Append('<').Append(name);
            if (style.Length > 0) output.Append(" style=\"").Append(style).Append('"');
            output.Append('>');
            if (m.Groups["self"].Success) output.Append("</").Append(name).Append('>');
            else open.Add(name);
        }
        for (var k = open.Count - 1; k >= 0; k--) output.Append("</").Append(open[k]).Append('>');
        return output.ToString();
    }

    /// <summary>Chữ thuần (tìm kiếm, tóm tắt, email chữ thường, đếm độ dài): xuống dòng ở br / hết đoạn.</summary>
    public static string ToPlainText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (!IsRich(value)) return value.Trim();
        var text = LineBreak().Replace(value, "\n");
        text = AnyTag().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        return BlankLines().Replace(text, "\n\n").Trim();
    }

    private static string Style(string attrs)
    {
        var m = StyleAttr().Match(attrs);
        if (!m.Success) return "";
        var raw = WebUtility.HtmlDecode(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
        var kept = new List<string>();
        foreach (var decl in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = decl.IndexOf(':');
            if (colon <= 0) continue;
            var prop = decl[..colon].Trim().ToLowerInvariant();
            var value = decl[(colon + 1)..].Trim().ToLowerInvariant();
            var clean = prop switch
            {
                "color" => Color().IsMatch(value) ? value : null,
                "font-size" => FontSize().Match(value) is { Success: true } px && int.Parse(px.Groups[1].Value) is >= 10 and <= 40 ? value : null,
                "font-weight" => value is "bold" or "bolder" or "600" or "700" or "800" or "900" ? "bold" : null,
                "font-style" => value == "italic" ? "italic" : null,
                "text-decoration" or "text-decoration-line" => Decoration(value),
                _ => null
            };
            if (clean is not null) kept.Add($"{(prop == "text-decoration-line" ? "text-decoration" : prop)}: {clean}");
        }
        return string.Join("; ", kept);
    }

    private static string? Decoration(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p is "underline" or "line-through").Distinct().ToList();
        return parts.Count > 0 ? string.Join(' ', parts) : null;
    }

    /// <summary>Chữ giữa các thẻ: giữ thực thể HTML hợp lệ (&amp;nbsp; …), mã hóa &lt; &gt; và &amp; lẻ.</summary>
    private static string EscapeText(string text) =>
        LoneAmp().Replace(text, "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    [GeneratedRegex(@"<!--[\s\S]*?-->|<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9]*)\b(?<attrs>(?:[^>""']|""[^""]*""|'[^']*')*?)(?<self>/)?>|[^<]+|<")]
    private static partial Regex Token();

    [GeneratedRegex(@"<\s*/?\s*(?:p|div|br|span|b|strong|i|em|u|ul|ol|li)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex RichTag();

    [GeneratedRegex(@"\bstyle\s*=\s*(""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttr();

    [GeneratedRegex(@"^(#[0-9a-f]{3}|#[0-9a-f]{6}|rgb\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\))$")]
    private static partial Regex Color();

    [GeneratedRegex(@"^(\d{1,2})px$")]
    private static partial Regex FontSize();

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li)>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"&(?!(?:[a-zA-Z][a-zA-Z0-9]{1,31}|#[0-9]{1,7}|#x[0-9a-fA-F]{1,6});)")]
    private static partial Regex LoneAmp();
}
