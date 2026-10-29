using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Staff;
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

    /// <summary>Giờ Việt Nam của <see cref="IdentityFixture.Clock"/> (03:00 UTC = 10:00).</summary>
    private static readonly DateTime Now = new(2026, 9, 29, 10, 0, 0);

    private static NotificationItemInput Item(string? title, string body, string? link = null, params string[] images) => new(title, body, images, link);

    private static SaveNotificationCommand Save(
        string title, IReadOnlyList<NotificationItemInput> items, bool publish = true, bool important = false, bool pinned = false, bool email = false,
        string audience = NotificationAudience.All, int[]? types = null, string[]? codes = null, DateTime? at = null, DateOnly? until = null, long? id = null) =>
        new(id, title, items, important, pinned, email, audience, types, codes, at, until, publish);

    private static SaveNotificationCommand Simple(string title, string body, bool important = false, bool email = false, string? link = null) =>
        Save(title, [Item(null, body, link)], important: important, email: email);

    private static IServiceProvider Services(IdentityFixture f) => new ServiceCollection()
        .AddSingleton(f.Db)
        .AddSingleton(f.Customers.Object)
        .AddSingleton<IEmailSender>(f.Email)
        .AddSingleton(f.NotificationImages)
        .AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Company:PortalUrl"] = "https://portal.test/" }).Build())
        .AddLogging()
        .BuildServiceProvider();

    private static async Task<IReadOnlyList<NotificationDto>> SeenBy(IdentityFixture f, long customerId)
    {
        f.CurrentUser.SetupGet(u => u.CustomerId).Returns(customerId);
        return (await f.GetNotifications.Handle(new GetNotificationsQuery(), Ct)).Value.Data;
    }

    private static void EmailOf(IdentityFixture f, params (long Id, string Email)[] customers) =>
        f.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<long> ids, CancellationToken _) =>
                customers.Where(c => ids.Contains(c.Id)).Select(c => new CustomerSummary(c.Id, $"KH{c.Id}", $"Công ty {c.Id}", null, c.Email, null, null, null)).ToList());

    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 1, 2, 3];

    [Fact]
    public async Task Khach_thay_thong_bao_moi_danh_dau_da_doc_rieng_tung_tai_khoan()
    {
        await using var f = new IdentityFixture();
        var created = await f.SaveNotification.Handle(Simple(" Nghỉ lễ 2/9 ", "Kho nghỉ 2 ngày.", important: true), Ct);
        Assert.True(created.IsSuccess, created.Error.Message);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await f.SaveNotification.Handle(Simple("Phụ phí xăng dầu", "Tăng 2%."), Ct);
        Assert.Empty(f.Jobs.Jobs);

        var list = (await f.GetNotifications.Handle(new GetNotificationsQuery(), Ct)).Value;
        Assert.Equal(2, list.UnreadCount);
        Assert.Equal(["Phụ phí xăng dầu", "Nghỉ lễ 2/9"], list.Data.Select(n => n.Title));
        Assert.True(list.Data[1].Imp);
        Assert.Equal("notice", list.Data[1].Kind);
        Assert.Equal("Kho nghỉ 2 ngày.", Assert.Single(list.Data[1].Items).Body);

        var id = long.Parse(created.Value.Id);
        Assert.True((await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(id), Ct)).IsSuccess);
        Assert.True((await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(id), Ct)).IsSuccess); // bấm lại không lỗi
        Assert.Equal(1, (await f.GetNotifications.Handle(new GetNotificationsQuery(), Ct)).Value.UnreadCount);

        // Tài khoản con có trạng thái đã đọc riêng.
        f.SignInAs(55);
        Assert.Equal(2, (await f.GetNotifications.Handle(new GetNotificationsQuery(), Ct)).Value.UnreadCount);
        Assert.Equal(2, (await f.MarkAllNotificationsRead.Handle(new MarkAllNotificationsReadCommand(), Ct)).Value);
        Assert.Equal(0, (await f.GetNotifications.Handle(new GetNotificationsQuery(), Ct)).Value.UnreadCount);

        var admin = Assert.Single(await f.GetAdminNotifications.Handle(new GetAdminNotificationsQuery(), Ct), n => n.Id == created.Value.Id);
        Assert.Equal((2, 1, 1), (admin.ReadCount, admin.ReadCustomers, admin.Recipients)); // 1 khách có mật khẩu portal trong fixture
        Assert.Equal("live", admin.Status);
        Assert.Equal(NotificationErrors.NotFound, (await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(999), Ct)).Error);
    }

    [Fact]
    public async Task Thong_bao_nhieu_tin_moi_tin_co_anh_va_link_rieng()
    {
        await using var f = new IdentityFixture();
        var image = NotificationImageStore.NameOf(await f.NotificationImages.SaveAsync(Png, Ct))!;

        var created = await f.SaveNotification.Handle(Save("Bản tin tuần 44",
        [
            Item("Phụ phí xăng dầu", "Từ 1/11 tăng 1%.", "https://vietanexpress.com.vn/phu-phi", image),
            Item("Lịch nghỉ lễ", "Kho nghỉ thứ 6."),
            Item(null, " ", null) // tin trống hoàn toàn bị bỏ
        ], pinned: true), Ct);
        Assert.True(created.IsSuccess, created.Error.Message);

        var seen = Assert.Single(await SeenBy(f, IdentityFixture.CustomerId));
        Assert.True(seen.Pin);
        Assert.Equal(["Phụ phí xăng dầu", "Lịch nghỉ lễ"], seen.Items.Select(i => i.Title));
        Assert.Equal([image], seen.Items[0].Images);
        Assert.Equal("https://vietanexpress.com.vn/phu-phi", seen.Items[0].Link);
        Assert.Null(seen.Items[1].Link);
        Assert.Contains("1. Phụ phí xăng dầu", seen.Body); // bản chữ tóm tắt trong Noi_Dung
        Assert.Equal($"/Upload/ThongBao/{image}", (await f.Db.Notifications.SingleAsync(Ct)).Items.OrderBy(i => i.SortOrder).First().Images);
    }

    [Fact]
    public async Task Email_thong_bao_nhieu_tin_kem_anh_va_link_tung_tin()
    {
        await using var f = new IdentityFixture();
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var image = NotificationImageStore.NameOf(await f.NotificationImages.SaveAsync(Png, Ct))!;
        await f.SaveNotification.Handle(Save("Bản tin", [Item("Tin A", "Nội dung A", "https://a.vn/x", image), Item("Tin B", "Nội dung B")], email: true), Ct);
        await f.Jobs.RunAllAsync(Services(f));

        var html = Assert.Single(f.Email.Sent).HtmlBody!;
        Assert.Contains(">Tin A</h2>", html);
        Assert.Contains(">Tin B</h2>", html);
        // Ảnh nhúng thẳng vào email (cid:) — Gmail hiện được dù máy chủ ảnh không truy cập được từ ngoài.
        Assert.Contains($"src=\"cid:{image}\"", html);
        var inline = Assert.Single(f.Email.Sent[0].Attachments!);
        Assert.Equal((image, "image/png"), (inline.ContentId, inline.ContentType));
        Assert.Equal(Png, inline.Data);
        Assert.Contains("href=\"https://a.vn/x\"", html);
        Assert.Contains("Xem chi tiết: https://a.vn/x", f.Email.Sent[0].Body);
    }

    [Fact]
    public async Task Gui_thu_email_cung_nhung_anh_cua_tin()
    {
        await using var f = new IdentityFixture();
        var image = NotificationImageStore.NameOf(await f.NotificationImages.SaveAsync(Png, Ct))!;
        await f.TestNotificationEmail.Handle(new TestNotificationEmailCommand("a@x.vn", "A", [Item(null, "B", null, image)], false), Ct);

        var mail = Assert.Single(f.Email.Sent);
        Assert.Contains($"src=\"cid:{image}\"", mail.HtmlBody);
        Assert.Equal(image, Assert.Single(mail.Attachments!).ContentId);
    }

    [Fact]
    public async Task Noi_dung_co_dinh_dang_va_noi_dung_chi_tiet_duoc_loc_va_hien_cho_khach()
    {
        await using var f = new IdentityFixture();
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var created = await f.SaveNotification.Handle(Save("Bản tin",
        [
            new NotificationItemInput("Phụ phí", "<p>Tăng <b>2%</b><script>x</script></p>", [], null,
                "<p>Chi tiết <span style=\"color: #dc2626\">bảng giá</span></p>"),
            new NotificationItemInput("Nghỉ lễ", "Kho nghỉ thứ 6", [], null, "<p><br></p>") // chi tiết trống → không có nút
        ], email: true), Ct);
        Assert.True(created.IsSuccess, created.Error.Message);

        var items = Assert.Single(await SeenBy(f, IdentityFixture.CustomerId)).Items;
        Assert.Equal("<p>Tăng <b>2%</b></p>", items[0].Body);
        Assert.Equal("<p>Chi tiết <span style=\"color: #dc2626\">bảng giá</span></p>", items[0].Detail);
        Assert.Null(items[1].Detail);
        Assert.Contains("1. Phụ phí\nTăng 2%", (await f.Db.Notifications.SingleAsync(Ct)).Body); // tóm tắt chữ thuần

        await f.Jobs.RunAllAsync(Services(f));
        var mail = Assert.Single(f.Email.Sent);
        Assert.Contains("Tăng <b>2%</b>", mail.HtmlBody);
        Assert.Contains($"https://portal.test/notifications?id={created.Value.Id}", mail.HtmlBody); // nút xem nội dung chi tiết trên portal
        Assert.Contains("Tăng 2%", mail.Body);
    }

    [Fact]
    public void Nut_xem_chi_tiet_chi_mo_link_hoac_noi_dung_chi_tiet()
    {
        var both = NotificationRules.ValidateContent("A", [new NotificationItemInput(null, "x", [], "https://a.vn", "<p>chi tiết</p>")]);
        Assert.Equal("Tin 1: nút Xem chi tiết chỉ mở link hoặc nội dung chi tiết, không cả hai", both.Error.Message);
        Assert.Equal("Tin 1: nhập nội dung (tối đa 4000 ký tự)",
            NotificationRules.ValidateContent("A", [new NotificationItemInput(null, "<p><b> </b></p>", ["a.png"], null)]).Error.Message);
    }

    [Fact]
    public async Task Anh_khong_co_tren_may_chu_thi_bao_loi()
    {
        await using var f = new IdentityFixture();
        var result = await f.SaveNotification.Handle(Save("A", [Item(null, "B", null, new string('a', 64) + ".png")]), Ct);
        Assert.Equal(NotificationErrors.ImageNotFound, result.Error);
        await Assert.ThrowsAsync<FormatException>(() => f.NotificationImages.SaveAsync("GIF89a"u8.ToArray(), Ct));
    }

    [Fact]
    public async Task Dang_theo_loai_khach_chi_khach_dung_loai_thay_va_nhan_email()
    {
        await using var f = new IdentityFixture();
        f.Db.Logins.Add(new CustomerLogin(301, "shop", "pw", customerTypeId: 3));
        f.Db.Logins.Add(new CustomerLogin(302, "daily", "pw", customerTypeId: 4));
        f.Db.Logins.Add(new CustomerLogin(303, "shop-chua-mk", "", customerTypeId: 3));
        f.Db.CustomerTypes.AddRange(new CustomerType(2, "Cá nhân"), new CustomerType(3, " Công Ty "));
        await f.Db.SaveChangesAsync(Ct);
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"), (301, "shop@x.vn"), (302, "daily@x.vn"));

        var created = await f.SaveNotification.Handle(Save("Ưu đãi shop", [Item(null, "Giảm 10%.")], email: true, audience: NotificationAudience.Types, types: [3]), Ct);
        Assert.Equal(1, created.Value.Recipients); // 303 chưa có mật khẩu portal
        Assert.Equal([3], created.Value.CustomerTypes);

        Assert.Single(await SeenBy(f, 301));
        Assert.Empty(await SeenBy(f, 302));
        Assert.Empty(await SeenBy(f, IdentityFixture.CustomerId)); // chưa phân loại

        await f.Jobs.RunAllAsync(Services(f));
        Assert.Equal(["shop@x.vn"], Assert.Single(f.Email.Sent).Bcc);

        var audience = await f.GetNotificationAudience.Handle(new GetNotificationAudienceQuery(), Ct);
        Assert.Equal(3, audience.Total);
        // Tên từ dbo.TCustomerType; loại chưa có khách vẫn hiện; mã loại không có trong danh mục → "Loại {id}".
        Assert.Equal([(2, "Cá nhân", 0), (3, "Công Ty", 1), (4, "Loại 4", 1)], audience.Types.Select(t => (t.Id, t.Name, t.Count)));
        Assert.Equal(NotificationErrors.TypesRequired,
            (await f.SaveNotification.Handle(Save("A", [Item(null, "B")], audience: NotificationAudience.Types, types: []), Ct)).Error);
    }

    [Fact]
    public async Task Gui_thu_email_chi_toi_dia_chi_nhap_khong_luu_thong_bao()
    {
        await using var f = new IdentityFixture();
        var items = new[] { Item("Tin 1", "<b>Đậm</b> chữ"), Item("Tin 2", "Chữ thường", "https://vietan.vn/a") };

        var sent = await f.TestNotificationEmail.Handle(new TestNotificationEmailCommand(" admin@vietan.vn; cs@vietan.vn ", "Đổi giá", items, true), Ct);

        Assert.Equal(2, sent.Value);
        var mail = Assert.Single(f.Email.Sent);
        Assert.Equal(["admin@vietan.vn", "cs@vietan.vn"], mail.To);
        Assert.Null(mail.Bcc);
        Assert.StartsWith("[Gửi thử] [Quan trọng] Đổi giá", mail.Subject);
        Assert.Contains("https://vietan.vn/a", mail.HtmlBody);
        Assert.Empty(f.Db.Notifications);
        Assert.Empty(f.Jobs.Jobs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-email")]
    [InlineData("a@x.vn, sai")]
    [InlineData("a@x.vn,b@x.vn,c@x.vn,d@x.vn,e@x.vn,f@x.vn")]
    public async Task Gui_thu_email_sai_dia_chi_bi_chan(string to)
    {
        await using var f = new IdentityFixture();
        var result = await f.TestNotificationEmail.Handle(new TestNotificationEmailCommand(to, "A", [Item(null, "B")], false), Ct);
        Assert.Equal(NotificationErrors.TestEmailInvalid, result.Error);
        Assert.Empty(f.Email.Sent);
    }

    [Fact]
    public async Task Gui_thu_email_khi_may_chu_chua_cau_hinh_email()
    {
        await using var f = new IdentityFixture();
        f.Email.IsConfigured = false;
        var result = await f.TestNotificationEmail.Handle(new TestNotificationEmailCommand("a@x.vn", "A", [Item(null, "B")], false), Ct);
        Assert.Equal(NotificationErrors.EmailNotConfigured, result.Error);
    }

    [Fact]
    public async Task Dang_cho_khach_chi_dinh_theo_ma_khach()
    {
        await using var f = new IdentityFixture();
        f.Db.Logins.Add(new CustomerLogin(301, "khac", "pw"));
        await f.Db.SaveChangesAsync(Ct);

        var unknown = await f.SaveNotification.Handle(Save("A", [Item(null, "B")], audience: NotificationAudience.Customers, codes: ["SaigonbayHN, KHONGCO"]), Ct);
        Assert.Equal(NotificationErrors.UnknownCustomers(["KHONGCO"]), unknown.Error);
        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));

        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var created = await f.SaveNotification.Handle(Save("Hồ sơ hải quan", [Item(null, "Bổ sung invoice.")], audience: NotificationAudience.Customers,
            codes: ["saigonbayhn", "SaigonbayHN"]), Ct);
        Assert.True(created.IsSuccess, created.Error.Message);
        Assert.Equal(1, created.Value.Recipients);
        Assert.Equal(["KH201008"], created.Value.CustomerCodes);
        Assert.Equal(",201008,", (await f.Db.Notifications.SingleAsync(Ct)).Recipients); // cột Khach_Nhan

        Assert.Single(await SeenBy(f, IdentityFixture.CustomerId));
        Assert.Empty(await SeenBy(f, 301));
    }

    [Fact]
    public async Task Nhap_va_hen_gio_chua_hien_toi_gio_thi_hien_va_gui_email()
    {
        await using var f = new IdentityFixture();
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var draft = await f.SaveNotification.Handle(Save("Nháp", [Item(null, "Đang soạn")], publish: false, email: true), Ct);
        Assert.Equal(("draft", NotificationEmailStatus.Scheduled), (draft.Value.Status, draft.Value.EmailStatus));
        var scheduled = await f.SaveNotification.Handle(Save("Bảng giá DHL", [Item(null, "Từ 1/10.")], email: true, at: Now.AddHours(2)), Ct);
        Assert.Equal(("scheduled", NotificationEmailStatus.Scheduled), (scheduled.Value.Status, scheduled.Value.EmailStatus));
        Assert.Empty(f.Jobs.Jobs);
        Assert.Empty(await SeenBy(f, IdentityFixture.CustomerId));

        Assert.Equal(0, await NotificationEmailJob.StartDueAsync(f.Db, f.Jobs, f.Clock, Ct));
        f.Clock.Now = f.Clock.Now.AddHours(2);
        Assert.Equal(1, await NotificationEmailJob.StartDueAsync(f.Db, f.Jobs, f.Clock, Ct)); // nháp không gửi
        await f.Jobs.RunAllAsync(Services(f));

        Assert.Equal("Bảng giá DHL", Assert.Single(await SeenBy(f, IdentityFixture.CustomerId)).Title);
        Assert.Equal("Bảng giá DHL — Việt An Express", Assert.Single(f.Email.Sent).Subject);
        Assert.Equal(0, await NotificationEmailJob.StartDueAsync(f.Db, f.Jobs, f.Clock, Ct)); // không gửi lại
    }

    [Fact]
    public async Task Het_han_thi_khach_khong_thay()
    {
        await using var f = new IdentityFixture();
        var created = await f.SaveNotification.Handle(Save("Bảo trì", [Item(null, "22h hôm nay")], until: DateOnly.FromDateTime(Now)), Ct);
        Assert.Single(await SeenBy(f, IdentityFixture.CustomerId));
        f.Clock.Now = f.Clock.Now.AddDays(1);
        Assert.Empty(await SeenBy(f, IdentityFixture.CustomerId));
        Assert.Equal("expired", Assert.Single(await f.GetAdminNotifications.Handle(new GetAdminNotificationsQuery(), Ct)).Status);

        Assert.Equal(NotificationErrors.ExpiresBeforePublish,
            (await f.SaveNotification.Handle(Save("A", [Item(null, "B")], until: DateOnly.FromDateTime(Now).AddDays(-1)), Ct)).Error);
        Assert.True(created.IsSuccess);
    }

    [Fact]
    public async Task Sua_giu_luot_doc_khong_gui_lai_email_go_va_dang_lai()
    {
        await using var f = new IdentityFixture();
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var created = (await f.SaveNotification.Handle(Save("Phụ phí", [Item(null, "Tăng 2%.")], email: true), Ct)).Value;
        await f.Jobs.RunAllAsync(Services(f));
        var id = long.Parse(created.Id);
        await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(id), Ct);

        var edited = await f.SaveNotification.Handle(Save("Phụ phí (sửa)", [Item("Tin 1", "Tăng 3%."), Item("Tin 2", "Áp dụng 1/11.")], email: true, id: id), Ct);
        Assert.True(edited.IsSuccess, edited.Error.Message);
        Assert.Empty(f.Jobs.Jobs);
        Assert.Equal((1, 2), (edited.Value.ReadCount, edited.Value.Items.Count));
        Assert.Equal(2, (await f.Db.Notifications.SingleAsync(Ct)).Items.Count); // các tin ghi đè trong cột JSON Tin

        var removed = await f.RemoveNotification.Handle(new RemoveNotificationCommand(id), Ct);
        Assert.Equal("removed", removed.Value.Status);
        Assert.Empty(await SeenBy(f, IdentityFixture.CustomerId));

        await f.SaveNotification.Handle(Save("Phụ phí (sửa)", [Item(null, "Tăng 3%.")], email: true, id: id), Ct);
        Assert.False(Assert.Single(await SeenBy(f, IdentityFixture.CustomerId)).Unread);
        Assert.Single(f.Email.Sent);
    }

    [Fact]
    public async Task Xoa_thong_bao_xoa_ca_luot_da_doc()
    {
        await using var f = new IdentityFixture();
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var id = long.Parse((await f.SaveNotification.Handle(
            Save("A", [Item(null, "B")], audience: NotificationAudience.Customers, codes: ["SaigonbayHN"]), Ct)).Value.Id);
        await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(id), Ct);

        Assert.True((await f.DeleteNotification.Handle(new DeleteNotificationCommand(id), Ct)).IsSuccess);

        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));
        Assert.Empty(await f.Db.NotificationReads.ToListAsync(Ct));
        Assert.Equal(NotificationErrors.NotFound, (await f.DeleteNotification.Handle(new DeleteNotificationCommand(id), Ct)).Error);
    }

    [Fact]
    public async Task Doc_danh_sach_khach_da_doc()
    {
        await using var f = new IdentityFixture();
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));
        var id = long.Parse((await f.SaveNotification.Handle(Simple("A", "B"), Ct)).Value.Id);
        await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(id), Ct);
        f.SignInAs(55);
        await f.MarkNotificationRead.Handle(new MarkNotificationReadCommand(id), Ct);

        var reader = Assert.Single((await f.GetNotificationReaders.Handle(new GetNotificationReadersQuery(id), Ct)).Value);
        Assert.Equal(("KH201008", "Công ty 201008", 2, "29/09/2026 10:00"), (reader.CustomerCode, reader.CompanyName, reader.Accounts, reader.ReadAt));
        Assert.Equal(NotificationErrors.NotFound, (await f.GetNotificationReaders.Handle(new GetNotificationReadersQuery(999), Ct)).Error);
    }

    [Theory]
    [InlineData("", "Nội dung")]
    [InlineData("Tiêu đề", "  ")]
    public async Task Thieu_tieu_de_hoac_noi_dung_thi_bao_loi(string title, string body)
    {
        await using var f = new IdentityFixture();
        Assert.True((await f.SaveNotification.Handle(Simple(title, body), Ct)).IsFailure);
        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));
    }

    [Fact]
    public void Kiem_tra_noi_dung_cac_tin()
    {
        Assert.Equal("Tin 2: nhập tiêu đề tin (thông báo có nhiều tin)",
            NotificationRules.ValidateContent("A", [Item("Một", "x"), Item(null, "y")]).Error.Message);
        Assert.Equal("Tin 1: nhập nội dung (tối đa 4000 ký tự)", NotificationRules.ValidateContent("A", [Item("Một", "", null, "a.png")]).Error.Message);
        Assert.Equal(NotificationErrors.ItemsRequired, NotificationRules.ValidateContent("A", Enumerable.Range(0, 11).Select(i => Item($"T{i}", "x")).ToList()).Error);
        Assert.Equal(NotificationErrors.ItemsRequired, NotificationRules.ValidateContent("A", []).Error);
        Assert.Equal("Tin 1: tối đa 5 ảnh", NotificationRules.ValidateContent("A", [Item(null, "x", null, "1", "2", "3", "4", "5", "6")]).Error.Message);
        Assert.True(NotificationRules.ValidateContent("A", [Item(null, "chỉ 1 tin không cần tiêu đề")]).IsSuccess);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("vietanexpress.com.vn")]
    [InlineData("ftp://x.vn/a")]
    [InlineData("data:text/html,abc")]
    public async Task Tu_choi_duong_link_khong_phai_http(string link)
    {
        await using var f = new IdentityFixture();
        Assert.StartsWith("Tin 1: đường link", (await f.SaveNotification.Handle(Simple("A", "B", link: link), Ct)).Error.Message);
    }

    [Fact]
    public async Task Chua_cau_hinh_email_thi_khong_dang_kem_email_nhung_luu_nhap_duoc()
    {
        await using var f = new IdentityFixture();
        f.Email.IsConfigured = false;
        Assert.Equal(NotificationErrors.EmailNotConfigured, (await f.SaveNotification.Handle(Simple("A", "B", email: true), Ct)).Error);
        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct));
        Assert.True((await f.SaveNotification.Handle(Save("A", [Item(null, "B")], publish: false, email: true), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Gui_email_thong_bao_bcc_cho_khach_co_tai_khoan_va_nhan_vien_ghi_ket_qua()
    {
        await using var f = new IdentityFixture();
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "kho", "hash", "Kho", "kho@sgb.vn", null, [], DateTime.Now));
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "trung", "hash", "Trùng", "OPS@sgb.vn", null, [], DateTime.Now));
        f.Db.StaffAccounts.Add(new StaffAccount(777, "khac", "hash", "Khách khác", "khac@x.vn", null, [], DateTime.Now));
        await f.Db.SaveChangesAsync(Ct);
        EmailOf(f, (IdentityFixture.CustomerId, "ops@sgb.vn"));

        var created = await f.SaveNotification.Handle(Simple("Lịch cut-off", "Thứ 6 cut-off 15:00", email: true), Ct);
        Assert.Equal(NotificationEmailStatus.Sending, created.Value.EmailStatus);
        await f.Jobs.RunAllAsync(Services(f));

        var mail = Assert.Single(f.Email.Sent);
        Assert.Equal("Lịch cut-off — Việt An Express", mail.Subject);
        Assert.Contains("Thứ 6 cut-off 15:00", mail.HtmlBody);
        Assert.Equal(["ops@sgb.vn", "kho@sgb.vn"], mail.Bcc); // nhân viên của khách không nhận thông báo thì không gửi
        Assert.Contains("https://portal.test/notifications", mail.HtmlBody);
        var saved = await f.Db.Notifications.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((NotificationEmailStatus.Sent, 2, 2), (saved.EmailStatus, saved.EmailRecipients, saved.EmailSent));
    }

    [Fact]
    public void Ma_khach_nhap_tay_tach_dau_phay_xuong_dong_bo_trung()
    {
        Assert.Equal(["KH1", "KH2", "KH3"], NotificationRules.CustomerCodes(["KH1, kh1;KH2", "\nKH3 "]));
    }

    [Fact]
    public void Nguoi_nhan_bo_email_sai_trung_va_cat_theo_gioi_han()
    {
        var (emails, skipped) = NotificationEmail.Recipients(["a@x.vn", " A@x.vn ", "sai", "b@x", null, "c@x.vn", "d@x.vn"], max: 2);
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
