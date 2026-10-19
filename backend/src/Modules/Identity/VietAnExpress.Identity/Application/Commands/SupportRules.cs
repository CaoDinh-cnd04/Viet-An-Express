using System.Net.Mail;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>Phần dùng chung của email gửi về hộp thư Việt An (yêu cầu hỗ trợ &amp; sự cố, sửa thông tin người gửi) — hàm thuần, có test.</summary>
internal static class SupportRules
{
    /// <summary>Địa chỉ trả lời: SĐT/email khách nhập là email hợp lệ thì dùng, không thì email hồ sơ khách.</summary>
    public static string? ReplyTo(string? contact, string? profileEmail) =>
        IsEmail(contact) ? contact!.Trim() : IsEmail(profileEmail) ? profileEmail!.Trim() : null;

    public static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value.Trim(), out var a) && a.Address == value.Trim();

    /// <summary>Bảng "Thông tin khách" trong email.</summary>
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

    /// <summary>Tên tệp an toàn để lưu / trả về: bỏ đường dẫn, tối đa 255 ký tự.</summary>
    public static string SafeFileName(string? name)
    {
        // Tên upload có thể chứa đường dẫn Windows dù server chạy Linux.
        var file = Path.GetFileName((name ?? "").Replace('\\', '/')).Trim();
        if (file.Length == 0) file = "tep-dinh-kem";
        return file.Length <= 255 ? file : file[^255..];
    }
}
