using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>Ghi lại việc nền thay vì chạy — test tự chạy bằng <see cref="RunAllAsync"/>.</summary>
internal sealed class RecordingJobQueue : IBackgroundJobQueue
{
    public List<(string Name, Func<IServiceProvider, CancellationToken, Task> Job)> Jobs { get; } = [];

    public void Enqueue(string name, Func<IServiceProvider, CancellationToken, Task> job) => Jobs.Add((name, job));

    public async Task RunAllAsync(IServiceProvider services)
    {
        foreach (var (_, job) in Jobs) await job(services, CancellationToken.None);
        Jobs.Clear();
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public bool IsConfigured { get; set; } = true;
    public List<EmailMessage> Sent { get; } = [];

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return Task.FromResult(false);
        Sent.Add(message);
        return Task.FromResult(true);
    }
}

public class NotificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AdminNotificationHandlers Admin(IdentityFixture f) => new(f.Db, f.CurrentUser.Object, f.Email, f.Jobs, f.Clock);
    private static CustomerNotificationHandlers Customer(IdentityFixture f) => new(f.Db, f.CurrentUser.Object, f.Clock);

    private static IServiceProvider Services(IdentityFixture f) => new ServiceCollection()
        .AddSingleton(f.Db)
        .AddSingleton(f.Customers.Object)
        .AddSingleton<IEmailSender>(f.Email)
        .AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Company:PortalUrl"] = "https://portal.test/" }).Build())
        .AddLogging()
        .BuildServiceProvider();

    [Fact]
    public async Task Khach_thay_thong_bao_moi_danh_dau_da_doc_rieng_tung_tai_khoan()
    {
        await using var f = new IdentityFixture();
        var created = await Admin(f).Handle(new CreateNotificationCommand(" Nghỉ lễ 2/9 ", "Kho nghỉ 2 ngày.", true, false), Ct);
        Assert.True(created.IsSuccess, created.Error.Message);
        await Admin(f).Handle(new CreateNotificationCommand("Phụ phí xăng dầu", "Tăng 2%.", false, false), Ct);
        Assert.Empty(f.Jobs.Jobs);

        var list = (await Customer(f).Handle(new GetNotificationsQuery(), Ct)).Value;
        Assert.Equal(2, list.UnreadCount);
        Assert.Equal(["Phụ phí xăng dầu", "Nghỉ lễ 2/9"], list.Data.Select(n => n.Title));
        Assert.True(list.Data[1].Imp);

        var id = long.Parse(created.Value.Id);
        Assert.True((await Customer(f).Handle(new MarkNotificationReadCommand(id), Ct)).IsSuccess);
        Assert.True((await Customer(f).Handle(new MarkNotificationReadCommand(id), Ct)).IsSuccess); // bấm lại không lỗi
        Assert.Equal(1, (await Customer(f).Handle(new GetNotificationsQuery(), Ct)).Value.UnreadCount);

        // Tài khoản con có trạng thái đã đọc riêng.
        f.SignInAs(55);
        Assert.Equal(2, (await Customer(f).Handle(new GetNotificationsQuery(), Ct)).Value.UnreadCount);
        Assert.Equal(2, (await Customer(f).Handle(new MarkAllNotificationsReadCommand(), Ct)).Value);
        Assert.Equal(0, (await Customer(f).Handle(new GetNotificationsQuery(), Ct)).Value.UnreadCount);

        Assert.Equal(2, Assert.Single(await Admin(f).Handle(new GetAdminNotificationsQuery(), Ct), n => n.Id == created.Value.Id).ReadCount);
        Assert.Equal(NotificationErrors.NotFound, (await Customer(f).Handle(new MarkNotificationReadCommand(999), Ct)).Error);
    }

    [Fact]
    public async Task Xoa_thong_bao_xoa_ca_luot_da_doc()
    {
        await using var f = new IdentityFixture();
        var id = long.Parse((await Admin(f).Handle(new CreateNotificationCommand("A", "B", false, false), Ct)).Value.Id);
        await Customer(f).Handle(new MarkNotificationReadCommand(id), Ct);

        Assert.True((await Admin(f).Handle(new DeleteNotificationCommand(id), Ct)).IsSuccess);

        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));
        Assert.Empty(await f.Db.NotificationReads.ToListAsync(Ct));
        Assert.Equal(NotificationErrors.NotFound, (await Admin(f).Handle(new DeleteNotificationCommand(id), Ct)).Error);
    }

    [Theory]
    [InlineData("", "Nội dung")]
    [InlineData("Tiêu đề", "  ")]
    public async Task Thieu_tieu_de_hoac_noi_dung_thi_bao_loi(string title, string body)
    {
        await using var f = new IdentityFixture();
        Assert.True((await Admin(f).Handle(new CreateNotificationCommand(title, body, false, false), Ct)).IsFailure);
        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));
    }

    [Fact]
    public async Task Chua_cau_hinh_email_thi_khong_dang_thong_bao_kem_email()
    {
        await using var f = new IdentityFixture();
        f.Email.IsConfigured = false;
        Assert.Equal(NotificationErrors.EmailNotConfigured, (await Admin(f).Handle(new CreateNotificationCommand("A", "B", false, true), Ct)).Error);
        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));
    }

    [Fact]
    public async Task Gui_email_thong_bao_bcc_cho_khach_co_tai_khoan_va_nhan_vien_ghi_ket_qua()
    {
        await using var f = new IdentityFixture();
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "kho", "hash", "Kho", "kho@sgb.vn", null, [], DateTime.Now));
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "trung", "hash", "Trùng", "OPS@sgb.vn", null, [], DateTime.Now));
        await f.Db.SaveChangesAsync(Ct);
        f.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<long> ids, CancellationToken _) =>
                ids.Contains(IdentityFixture.CustomerId)
                    ? [new CustomerSummary(IdentityFixture.CustomerId, "SGB", "SGB", null, "ops@sgb.vn", null, null, null)]
                    : []);

        var created = await Admin(f).Handle(new CreateNotificationCommand("Lịch cut-off", "Thứ 6 cut-off 15:00", false, true), Ct);
        Assert.Equal(NotificationEmailStatus.Sending, created.Value.EmailStatus);
        await f.Jobs.RunAllAsync(Services(f));

        var mail = Assert.Single(f.Email.Sent);
        Assert.Equal("Lịch cut-off — Việt An Express", mail.Subject);
        Assert.Contains("Thứ 6 cut-off 15:00", mail.HtmlBody);
        Assert.Equal(["ops@sgb.vn", "kho@sgb.vn"], mail.Bcc);
        Assert.Contains("https://portal.test/notifications", mail.HtmlBody);
        var saved = await f.Db.Notifications.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((NotificationEmailStatus.Sent, 2, 2), (saved.EmailStatus, saved.EmailRecipients, saved.EmailSent));
    }

    [Fact]
    public async Task Thong_bao_co_duong_link_khach_thay_va_email_kem_link()
    {
        await using var f = new IdentityFixture();
        f.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CustomerSummary(IdentityFixture.CustomerId, "SGB", "SGB", null, "ops@sgb.vn", null, null, null)]);

        var created = await Admin(f).Handle(
            new CreateNotificationCommand("Bảng giá mới", "Áp dụng từ 1/11.", false, true, " https://vietanexpress.com.vn/bang-gia "), Ct);
        Assert.Equal("https://vietanexpress.com.vn/bang-gia", created.Value.Link);
        await f.Jobs.RunAllAsync(Services(f));

        Assert.Equal("https://vietanexpress.com.vn/bang-gia", Assert.Single((await Customer(f).Handle(new GetNotificationsQuery(), Ct)).Value.Data).Link);
        Assert.Contains("Xem tại đây: https://vietanexpress.com.vn/bang-gia", Assert.Single(f.Email.Sent).Body);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("vietanexpress.com.vn")]
    [InlineData("ftp://x.vn/a")]
    [InlineData("data:text/html,abc")]
    public async Task Tu_choi_duong_link_khong_phai_http(string link)
    {
        await using var f = new IdentityFixture();
        Assert.Equal(NotificationErrors.Link, (await Admin(f).Handle(new CreateNotificationCommand("A", "B", false, false, link), Ct)).Error);
    }

    [Fact]
    public void Nguoi_nhan_bo_email_sai_trung_va_cat_theo_gioi_han()
    {
        var (emails, skipped) = NotificationRules.Recipients(["a@x.vn", " A@x.vn ", "sai", "b@x", null, "c@x.vn", "d@x.vn"], max: 2);
        Assert.Equal(["a@x.vn", "c@x.vn"], emails);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void Ket_qua_gui_email()
    {
        var n = Notification.Create("A", "B", false, true, "admin", DateTime.Now);
        n.RecordEmail(0, 0, null);
        Assert.Equal(NotificationEmailStatus.NoRecipients, n.EmailStatus);
        n.RecordEmail(100, 50, "lỗi lô 2");
        Assert.Equal(NotificationEmailStatus.Partial, n.EmailStatus);
        n.RecordEmail(100, 0, "lỗi");
        Assert.Equal(NotificationEmailStatus.Failed, n.EmailStatus);
    }
}

public class SupportTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SendSupportRequestCommand Request(SupportAttachment? attachment = null, string contact = "khach@abc.vn") =>
        new("Báo lỗi phần mềm", "Không in được nhãn", "Bấm In & cấp bill thì báo lỗi mạng, thử lại vẫn vậy.", contact, attachment);

    [Fact]
    public async Task Gui_yeu_cau_ho_tro_xep_email_ve_hop_thu_kem_tep_va_dia_chi_tra_loi()
    {
        await using var f = new IdentityFixture();
        var handler = new SupportHandlers(f.CurrentUser.Object, f.Customers.Object, f.Email, f.Jobs, f.Company);
        var file = new SupportAttachment("loi.png", "image/png", [1, 2, 3]);

        Assert.True((await handler.Handle(Request(file), Ct)).IsSuccess);
        await f.Jobs.RunAllAsync(new ServiceCollection().AddSingleton<IEmailSender>(f.Email).BuildServiceProvider());

        var mail = Assert.Single(f.Email.Sent);
        Assert.Null(mail.To); // về hộp thư nhận mặc định
        Assert.Equal("khach@abc.vn", mail.ReplyTo);
        Assert.Contains("Không in được nhãn", mail.Subject);
        Assert.Contains("SGB EXPRESS HN", mail.Body);
        Assert.Equal("loi.png", Assert.Single(mail.Attachments!).FileName);
    }

    [Fact]
    public async Task Chua_cau_hinh_email_thi_bao_khach_goi_hotline()
    {
        await using var f = new IdentityFixture();
        f.Email.IsConfigured = false;
        var handler = new SupportHandlers(f.CurrentUser.Object, f.Customers.Object, f.Email, f.Jobs, f.Company);
        Assert.Equal(SupportErrors.EmailNotConfigured, (await handler.Handle(Request(), Ct)).Error);
        Assert.Empty(f.Jobs.Jobs);
    }

    [Fact]
    public void Kiem_tra_yeu_cau_ho_tro()
    {
        Assert.Null(SupportRules.Validate(Request()));
        Assert.Equal(SupportErrors.Subject, SupportRules.Validate(Request() with { Subject = "abc" }));
        Assert.Equal(SupportErrors.Message, SupportRules.Validate(Request() with { Message = "ngắn" }));
        Assert.Equal(SupportErrors.Contact, SupportRules.Validate(Request(contact: "09")));
        Assert.Equal(SupportErrors.AttachmentType, SupportRules.Validate(Request(new SupportAttachment("x.exe", "application/octet-stream", [1]))));
        Assert.Equal(SupportErrors.AttachmentTooLarge,
            SupportRules.Validate(Request(new SupportAttachment("x.pdf", "application/pdf", new byte[SupportRules.AttachmentMaxBytes + 1]))));
    }

    [Theory]
    [InlineData("0909123456", "ops@sgb.vn", "ops@sgb.vn")]
    [InlineData("khach@abc.vn", "ops@sgb.vn", "khach@abc.vn")]
    [InlineData("0909123456", null, null)]
    public void Dia_chi_tra_loi(string contact, string? profile, string? expected) => Assert.Equal(expected, SupportRules.ReplyTo(contact, profile));
}
