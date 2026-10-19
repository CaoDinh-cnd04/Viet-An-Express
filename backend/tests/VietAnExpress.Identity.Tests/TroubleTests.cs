using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Shipments.Contracts;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class TroubleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Ctx : IAsyncDisposable
    {
        public IdentityFixture F { get; } = new();
        public Mock<IShipmentsApi> Shipments { get; } = new();

        public Ctx()
        {
            Shipments.Setup(s => s.FindBillAsync(IdentityFixture.CustomerId, "VAHCM6003585US", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BillSummary("VAHCM6003585US", "John Smith", "United States"));
            F.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new CustomerSummary(IdentityFixture.CustomerId, "SaigonbayHN", "SGB EXPRESS HN", null, null, null, null, null)]);
        }

        public CustomerTroubleHandlers Customer => new(F.Db, F.CurrentUser.Object, F.Customers.Object, Shipments.Object, F.Jobs, F.Company, F.Clock);
        public AdminTroubleHandlers Admin => new(F.Db, F.CurrentUser.Object, F.Customers.Object, F.Company, F.Clock);
        public CustomerNotificationHandlers Notifications => new(F.Db, F.CurrentUser.Object, F.Clock);

        public async Task<TroubleDto> CreateAsync(NewTroubleRequest? request = null, TroubleFile? file = null)
        {
            var r = await Customer.Handle(new CreateTroubleCommand(request ?? Request(), file), Ct);
            Assert.True(r.IsSuccess, r.Error.Message);
            return r.Value;
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    private static NewTroubleRequest Request(string bill = "VAHCM6003585US") =>
        new(null, bill, null, null, "Giao chậm / trễ hẹn", null, "mid", "Đơn đi Mỹ quá 10 ngày chưa giao.", "Lan", "lan@sgb.vn");

    private static NewTroubleRequest Support(string? subject = "Sửa địa chỉ người gửi") =>
        new(TroubleKind.Support, null, null, null, "Sửa thông tin người gửi", subject, null, "Đổi địa chỉ sang 12 Lý Thường Kiệt, Hà Nội.", null, "0909123456");

    private static NewTroubleRequest Feedback() =>
        new(TroubleKind.Feedback, null, null, null, null, null, null, "Nên thêm nút sao chép mã bill.", null, null);

    [Fact]
    public async Task Khach_bao_su_co_ghi_vao_TContact_tu_dien_nguoi_nhan_va_bao_ve_hop_thu()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();

        Assert.StartsWith("TRB-", t.Id);
        Assert.Equal(("su_co", "VAHCM6003585US", "John Smith", "United States", "new"), (t.Kind, t.Bill, t.Cnee, t.Ct, t.Status));
        Assert.Contains(t.Id, Assert.Single(c.F.Jobs.Jobs).Name);
        // Phần mềm cũ đọc: FormType "Portal - Sự cố", Title = loại sự cố, Email lấy từ liên hệ là email.
        var row = await c.F.Db.Troubles.SingleAsync(Ct);
        Assert.Equal(("Giao chậm / trễ hẹn", "lan@sgb.vn"), (row.Title, row.Email));
        Assert.Equal(TroubleErrors.BillNotFound, (await c.Customer.Handle(new CreateTroubleCommand(Request("VAKHAC")), Ct)).Error);
    }

    [Fact]
    public async Task Admin_doi_trang_thai_kem_ghi_chu_thi_rieng_khach_do_nhan_thong_bao()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();

        var replied = await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, "Đã liên hệ hãng, dự kiến giao 15/10.", TroubleStatus.Doing), Ct);

        Assert.True(replied.IsSuccess, replied.Error.Message);
        Assert.Equal(("doing", "SaigonbayHN"), (replied.Value.Ticket.Status, replied.Value.CustomerCode));
        var notice = Assert.Single((await c.Notifications.Handle(new GetNotificationsQuery(), Ct)).Value.Data);
        Assert.Equal($"Sự cố {t.Id}: Đang xử lý", notice.Title);
        Assert.Contains("dự kiến giao 15/10", notice.Body);
        Assert.Equal($"https://portal.test/troubles?id={t.Id}", notice.Link);

        // Khách khác không thấy; trang quản trị Thông báo không lẫn thông báo riêng.
        c.F.CurrentUser.SetupGet(u => u.CustomerId).Returns(999);
        Assert.Empty((await c.Notifications.Handle(new GetNotificationsQuery(), Ct)).Value.Data);
        Assert.Empty(await new AdminNotificationHandlers(c.F.Db, c.F.CurrentUser.Object, c.F.Email, c.F.Jobs, c.F.Clock)
            .Handle(new GetAdminNotificationsQuery(), Ct));
    }

    [Fact]
    public async Task Tai_khoan_con_chi_thay_yeu_cau_minh_gui()
    {
        await using var c = new Ctx();
        await c.CreateAsync(); // tài khoản chính gửi
        c.F.SignInAs(7);
        var mine = await c.CreateAsync();

        Assert.Equal([mine.Id], (await c.Customer.Handle(new GetTroublesQuery(), Ct)).Value.Select(t => t.Id));
        c.F.SignInAs(null);
        Assert.Equal(2, (await c.Customer.Handle(new GetTroublesQuery(), Ct)).Value.Count);
    }

    [Fact]
    public async Task Admin_khong_gui_gi_thi_bao_loi_va_khong_tao_thong_bao()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();
        Assert.Equal(TroubleErrors.NothingToSend, (await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, " ", null), Ct)).Error);
        Assert.Equal(TroubleErrors.NothingToSend, (await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, null, TroubleStatus.New), Ct)).Error);
        Assert.Equal(TroubleErrors.Status, (await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, "x", "abc"), Ct)).Error);
        Assert.Empty(await c.F.Db.Notifications.ToListAsync(Ct));
    }

    [Fact]
    public async Task Sua_thong_tin_nguoi_gui_la_yeu_cau_ho_tro_tep_chi_gui_kem_email()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync(Support(), new TroubleFile(@"C:\fakepath\giay-phep.pdf", "application/pdf", [1, 2, 3]));

        Assert.Equal(("ho_tro", "", "Sửa địa chỉ người gửi", "Sửa thông tin người gửi", "low"), (t.Kind, t.Bill, t.Subject, t.Type, t.Lv));
        Assert.EndsWith("Đính kèm (đã gửi qua email): giay-phep.pdf", t.Desc);
        c.Shipments.Verify(s => s.FindBillAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await c.F.Jobs.RunAllAsync(new ServiceCollection().AddSingleton<IEmailSender>(c.F.Email).BuildServiceProvider());
        var mail = Assert.Single(c.F.Email.Sent);
        Assert.Contains("Sửa địa chỉ người gửi", mail.Subject);
        Assert.Equal("giay-phep.pdf", Assert.Single(mail.Attachments!).FileName);
    }

    [Fact]
    public async Task Gop_y_chi_can_noi_dung()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync(Feedback());
        Assert.Equal(("gop_y", TroubleRules.FeedbackType), (t.Kind, t.Type));
        Assert.Single(await c.Admin.Handle(new GetAdminTroublesQuery(), Ct));
    }

    [Theory]
    [InlineData("TRB-12", 12)]
    [InlineData("trb-7", 7)]
    [InlineData("15", 15)]
    [InlineData("TRB-", null)]
    [InlineData("abc", null)]
    public void Doc_ma_yeu_cau(string code, int? expected) => Assert.Equal(expected, Trouble.ParseCode(code));

    [Theory]
    [InlineData("new", 0)]
    [InlineData("doing", 1)]
    [InlineData("waitc", 2)]
    [InlineData("done", 3)]
    public void Trang_thai_luu_so_o_TContact(string status, int number)
    {
        Assert.Equal(number, TroubleStatus.ToNumber(status));
        Assert.Equal(status, TroubleStatus.FromNumber(number));
    }

    [Fact]
    public void FormType_cua_portal()
    {
        Assert.Equal("Portal - Hỗ trợ", TroubleKind.FormTypeOf(TroubleKind.Support));
        Assert.Equal(TroubleKind.Feedback, TroubleKind.FromFormType("Portal - Góp ý"));
    }

    [Fact]
    public void Kiem_tra_gui_yeu_cau()
    {
        Assert.Null(TroubleRules.Validate(Request()));
        Assert.Equal(TroubleErrors.Description, TroubleRules.Validate(Request() with { Desc = "ngắn" }));
        Assert.Equal(TroubleErrors.Priority, TroubleRules.Validate(Request() with { Lv = "urgent" }));
        Assert.Equal(TroubleErrors.Bill, TroubleRules.Validate(Request(" ")));
        Assert.Equal(TroubleErrors.Kind, TroubleRules.Validate(Request() with { Kind = "khac" }));
        Assert.Null(TroubleRules.Validate(Support()));
        Assert.Equal(TroubleErrors.Subject, TroubleRules.Validate(Support("abc")));
        Assert.Equal(TroubleErrors.Contact, TroubleRules.Validate(Support() with { Contact = " " }));
        Assert.Null(TroubleRules.Validate(Feedback()));
        Assert.Equal(TroubleErrors.FileType, TroubleRules.Validate(Support(), new TroubleFile("x.exe", "application/octet-stream", [1])));
        Assert.Equal(TroubleErrors.FileTooLarge, TroubleRules.Validate(Support(), new TroubleFile("x.pdf", "application/pdf", new byte[Trouble.FileMaxBytes + 1])));
    }

    [Theory]
    [InlineData("khach@abc.vn", "hoso@abc.vn", "khach@abc.vn")]
    [InlineData("0909123456", "hoso@abc.vn", "hoso@abc.vn")]
    [InlineData("0909123456", null, null)]
    public void Dia_chi_tra_loi(string contact, string? profile, string? expected) => Assert.Equal(expected, SupportRules.ReplyTo(contact, profile));

    [Theory]
    [InlineData(@"C:\Users\a\anh.png", "anh.png")]
    [InlineData("  ", "tep-dinh-kem")]
    public void Ten_tep_an_toan(string name, string expected) => Assert.Equal(expected, SupportRules.SafeFileName(name));
}
