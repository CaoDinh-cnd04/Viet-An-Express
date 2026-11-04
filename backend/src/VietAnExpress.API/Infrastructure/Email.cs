using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.API.Infrastructure;

/// <summary>
/// Cấu hình gửi email (mục <c>Email</c>). Mật khẩu chỉ để trong user-secrets / biến môi trường (<c>Email__Password</c>).
/// Từ 03/11/2026 gửi bằng hộp thư support@vietanexpress.com (cPanel, máy chủ pro43.emailserver.vn): STARTTLS cổng 587,
/// mật khẩu của hộp thư. (SmtpClient không hỗ trợ SSL ngầm cổng 465 — dùng 587.)
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    public string Host { get; set; } = "pro43.emailserver.vn";
    public int Port { get; set; } = 587;
    /// <summary>Tài khoản gửi (cũng là địa chỉ người gửi).</summary>
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromName { get; set; } = "Việt An Express";
    /// <summary>
    /// Hộp thư nhận góp ý / yêu cầu hỗ trợ của khách — nhiều địa chỉ ngăn bằng dấu phẩy / chấm phẩy
    /// (vd "cs@vietanexpress.com, sale@vietanexpress.com"); trống thì dùng <see cref="UserName"/>.
    /// </summary>
    public string InboxAddress { get; set; } = "";

    /// <summary>Danh sách địa chỉ nhận của <see cref="InboxAddress"/> (bỏ trống / trùng); trống thì <see cref="UserName"/>.</summary>
    public IReadOnlyList<string> InboxAddresses()
    {
        var list = InboxAddress.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list.Count > 0 ? list : [UserName.Trim()];
    }
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Gửi email qua SMTP (STARTTLS cổng 587), kèm bản HTML nếu có. Mỗi lần gửi mở 1 kết nối — lượng email của portal nhỏ.</summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private EmailOptions O => options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(O.UserName) && !string.IsNullOrWhiteSpace(O.Password);

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Chưa cấu hình Email:UserName / Email:Password — bỏ qua email \"{Subject}\"", message.Subject);
            return false;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(O.UserName.Trim(), O.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        var to = message.To is { Count: > 0 } ? message.To : O.InboxAddresses();
        foreach (var address in to) mail.To.Add(address.Trim());
        foreach (var address in message.Bcc ?? []) mail.Bcc.Add(address.Trim());
        if (!string.IsNullOrWhiteSpace(message.ReplyTo)) mail.ReplyToList.Add(message.ReplyTo.Trim());
        // Bản HTML đi kèm bản chữ thuần: trình đọc thư chọn bản hiển thị được.
        // Ảnh nhúng (ContentId) đi cùng bản HTML (multipart/related); không có bản HTML thì gửi như tệp đính kèm thường.
        var html = string.IsNullOrEmpty(message.HtmlBody) ? null : AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, "text/html");
        if (html is not null) mail.AlternateViews.Add(html);
        foreach (var a in message.Attachments ?? [])
        {
            if (html is not null && !string.IsNullOrEmpty(a.ContentId))
                html.LinkedResources.Add(new LinkedResource(new MemoryStream(a.Data), a.ContentType) { ContentId = a.ContentId });
            else
                mail.Attachments.Add(new Attachment(new MemoryStream(a.Data), a.FileName, a.ContentType));
        }

        using var smtp = new SmtpClient(O.Host, O.Port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(O.UserName.Trim(), O.Password.Replace(" ", "")),
            Timeout = O.TimeoutSeconds * 1000
        };
        await smtp.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("Đã gửi email \"{Subject}\" tới {To} người nhận, {Bcc} BCC", message.Subject, mail.To.Count, mail.Bcc.Count);
        return true;
    }
}

/// <summary>Hàng đợi việc nền trong bộ nhớ (<see cref="IBackgroundJobQueue"/>).</summary>
internal sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<(string Name, Func<IServiceProvider, CancellationToken, Task> Job)> _channel =
        Channel.CreateUnbounded<(string, Func<IServiceProvider, CancellationToken, Task>)>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(string name, Func<IServiceProvider, CancellationToken, Task> job) => _channel.Writer.TryWrite((name, job));

    public ChannelReader<(string Name, Func<IServiceProvider, CancellationToken, Task> Job)> Reader => _channel.Reader;
}

/// <summary>Chạy lần lượt việc trong <see cref="BackgroundJobQueue"/>, mỗi việc 1 scope DI riêng; lỗi chỉ ghi log.</summary>
internal sealed class BackgroundJobWorker(BackgroundJobQueue queue, IServiceScopeFactory scopes, ILogger<BackgroundJobWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (name, job) in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await job(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Lỗi việc nền {Job}", name);
            }
        }
    }
}
