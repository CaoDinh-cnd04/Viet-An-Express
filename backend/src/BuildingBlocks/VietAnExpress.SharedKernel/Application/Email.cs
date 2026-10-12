namespace VietAnExpress.SharedKernel.Application;

/// <summary>Tệp đính kèm email (đã đọc vào bộ nhớ).</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Data);

/// <summary>
/// 1 email: <see cref="Body"/> chữ thuần, <see cref="HtmlBody"/> (nếu có) là bản trình bày — gửi cả 2 (multipart/alternative).
/// <see cref="To"/> trống → gửi về hộp thư nhận của Việt An (<c>Email:InboxAddress</c>).
/// <see cref="Bcc"/>: gửi hàng loạt mà người nhận không thấy địa chỉ của nhau.
/// </summary>
public sealed record EmailMessage(
    string Subject,
    string Body,
    IReadOnlyList<string>? To = null,
    IReadOnlyList<string>? Bcc = null,
    string? ReplyTo = null,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    string? HtmlBody = null);

/// <summary>Gửi email qua SMTP (cấu hình <c>Email:*</c>). Chưa cấu hình → không gửi, trả false.</summary>
public interface IEmailSender
{
    /// <summary>Đã cấu hình tài khoản gửi (Email:UserName + Email:Password).</summary>
    bool IsConfigured { get; }

    /// <summary>Gửi ngay (chờ SMTP). Lỗi SMTP → ném exception. Chưa cấu hình → trả false, không gửi.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Việc chạy nền sau khi request đã trả về (gửi email…): mỗi việc chạy trong 1 scope DI riêng, lỗi chỉ ghi log.
/// Hàng đợi trong bộ nhớ — mất nếu máy chủ khởi động lại đúng lúc đang chờ.
/// </summary>
public interface IBackgroundJobQueue
{
    void Enqueue(string name, Func<IServiceProvider, CancellationToken, Task> job);
}

public static class BackgroundJobQueueExtensions
{
    /// <summary>Gửi email ở nền — không làm chậm / hỏng request nếu SMTP lỗi.</summary>
    public static void EnqueueEmail(this IBackgroundJobQueue queue, string name, EmailMessage message) =>
        queue.Enqueue(name, (services, ct) =>
            ((IEmailSender)services.GetService(typeof(IEmailSender))!).SendAsync(message, ct));
}
