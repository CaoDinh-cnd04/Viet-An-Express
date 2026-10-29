using System.Globalization;
using System.Text.RegularExpressions;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Trao đổi giữa CS và khách trên 1 yêu cầu — không có bảng riêng, nối vào cuối dbo.TContact.Content (phần mềm cũ mở yêu cầu cũng đọc được):
/// <code>
/// &lt;nội dung khách gửi&gt;
///
/// --- CS Việt An · 28/10/2026 16:50
/// &lt;CS trả lời&gt;
///
/// --- Khách phản hồi · 28/10/2026 17:05
/// &lt;khách trả lời&gt;
/// </code>
/// Dòng bắt đầu bằng "---" trong chữ người dùng gõ được đổi thành "—" để không giả được dòng mở đầu. Hàm thuần — có test.
/// </summary>
internal static partial class SupportThread
{
    public const string FromCs = "cs";
    public const string FromCustomer = "customer";
    public const int ReplyMaxLength = 4000;

    private const string CsHeader = "CS Việt An";
    private const string CustomerHeader = "Khách phản hồi";
    private const string TimeFormat = "dd/MM/yyyy HH:mm";

    [GeneratedRegex(@"\n\n--- (CS Việt An|Khách phản hồi) · (\d{2}/\d{2}/\d{4} \d{2}:\d{2})\n")]
    private static partial Regex Header();

    [GeneratedRegex(@"(?m)^-{3,}")]
    private static partial Regex Dashes();

    /// <summary>Chữ người dùng gõ: bỏ khoảng trắng đầu / cuối, dòng "---…" đổi thành "—".</summary>
    public static string Clean(string text) => Dashes().Replace(text.Replace("\r\n", "\n").Trim(), "—");

    /// <summary>Đoạn nối vào Content cho 1 lần trả lời.</summary>
    public static string Block(string from, string text, DateTime at) =>
        $"\n\n--- {(from == FromCs ? CsHeader : CustomerHeader)} · {at.ToString(TimeFormat, CultureInfo.InvariantCulture)}\n{Clean(text)}";

    /// <summary>Tách Content thành nội dung khách gửi ban đầu + các lần trả lời (cũ trước, mới sau).</summary>
    public static (string Description, IReadOnlyList<SupportMessageDto> Messages) Parse(string content)
    {
        var headers = Header().Matches(content);
        if (headers.Count == 0) return (content, []);
        var messages = new List<SupportMessageDto>(headers.Count);
        for (var i = 0; i < headers.Count; i++)
        {
            var h = headers[i];
            var start = h.Index + h.Length;
            var end = i + 1 < headers.Count ? headers[i + 1].Index : content.Length;
            messages.Add(new SupportMessageDto(h.Groups[1].Value == CsHeader ? FromCs : FromCustomer, h.Groups[2].Value, content[start..end].Trim()));
        }
        return (content[..headers[0].Index], messages);
    }
}
