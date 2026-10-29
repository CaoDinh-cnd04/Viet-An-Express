using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.Contracts;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class SupportRequestTests
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

        public GetSupportRequestsHandler GetSupportRequests => new(F.Db, F.CurrentUser.Object);
        public CreateSupportRequestHandler CreateSupportRequest => new(F.Db, F.CurrentUser.Object, F.Customers.Object, Shipments.Object, F.Jobs, F.Company, F.Clock);
        public GetAdminSupportRequestsHandler GetAdminSupportRequests => new(F.Db, F.Customers.Object);
        public ReplySupportRequestHandler Reply => new(F.Db, F.Customers.Object, F.Clock);
        public AnswerSupportRequestHandler Answer => new(F.Db, F.CurrentUser.Object, F.Customers.Object, F.Jobs, F.Company, F.Clock);

        public async Task<SupportRequestDto> CreateAsync(NewSupportRequest? request = null, SupportRequestFile? file = null)
        {
            var r = await CreateSupportRequest.Handle(new CreateSupportRequestCommand(request ?? Request(), file), Ct);
            Assert.True(r.IsSuccess, r.Error.Message);
            return r.Value;
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    private static NewSupportRequest Request(string bill = "VAHCM6003585US") =>
        new(null, bill, null, null, "Giao chậm / trễ hẹn", null, "mid", "Đơn đi Mỹ quá 10 ngày chưa giao.", "Lan", "lan@sgb.vn");

    private static NewSupportRequest Support(string? subject = "Sửa địa chỉ người gửi") =>
        new(SupportRequestKind.Support, null, null, null, "Sửa thông tin người gửi", subject, null, "Đổi địa chỉ sang 12 Lý Thường Kiệt, Hà Nội.", null, "0909123456");

    private static NewSupportRequest Feedback() =>
        new(SupportRequestKind.Feedback, null, null, null, null, null, null, "Nên thêm nút sao chép mã bill.", null, null);

    [Fact]
    public async Task Khach_bao_su_co_ghi_vao_TContact_tu_dien_nguoi_nhan_va_bao_ve_hop_thu()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();

        Assert.StartsWith("TRB-", t.Id);
        Assert.Equal(("ho_tro", "VAHCM6003585US", "John Smith", "United States", "new"), (t.Kind, t.Bill, t.Cnee, t.Ct, t.Status)); // sự cố gộp vào hỗ trợ
        Assert.Contains(t.Id, Assert.Single(c.F.Jobs.Jobs).Name);
        // Phần mềm cũ đọc: FormType "Portal - Hỗ trợ", Title = vấn đề, Email lấy từ liên hệ là email.
        var row = await c.F.Db.SupportRequests.SingleAsync(Ct);
        Assert.Equal(("Giao chậm / trễ hẹn", "lan@sgb.vn"), (row.Title, row.Email));
        Assert.Equal(SupportRequestErrors.BillNotFound, (await c.CreateSupportRequest.Handle(new CreateSupportRequestCommand(Request("VAKHAC")), Ct)).Error);
    }

    [Fact]
    public async Task CS_tra_loi_cho_khach_khach_tra_loi_lai_chuyen_dang_xu_ly_khong_tao_thong_bao()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();

        var replied = await c.Reply.Handle(new ReplySupportRequestCommand(t.Id, "Nhờ chị gửi ảnh kiện.", SupportRequestStatus.WaitCustomer), Ct);
        Assert.True(replied.IsSuccess, replied.Error.Message);
        Assert.Equal(("waitc", "SaigonbayHN"), (replied.Value.Ticket.Status, replied.Value.CustomerCode));
        Assert.Equal(t.Desc, replied.Value.Ticket.Desc); // nội dung gốc tách riêng khỏi phần trao đổi
        Assert.Equal(("cs", "Nhờ chị gửi ảnh kiện."), (Assert.Single(replied.Value.Ticket.Thread).From, replied.Value.Ticket.Thread[0].Text));

        var jobs = c.F.Jobs.Jobs.Count;
        var answered = await c.Answer.Handle(new AnswerSupportRequestCommand(t.Id, "Em gửi ảnh qua email rồi ạ."), Ct);
        Assert.True(answered.IsSuccess, answered.Error.Message);
        Assert.Equal("doing", answered.Value.Status); // khách trả lời → Đang xử lý (vẫn ở Chưa xong)
        Assert.Equal(["cs", "customer"], answered.Value.Thread.Select(m => m.From));
        Assert.Equal(jobs + 1, c.F.Jobs.Jobs.Count); // báo email cho CS
        Assert.Equal(SupportRequestErrors.NotWaiting, (await c.Answer.Handle(new AnswerSupportRequestCommand(t.Id, "Thêm nữa"), Ct)).Error);
        Assert.Empty(await c.F.Db.Notifications.ToListAsync(Ct));
    }

    [Fact]
    public async Task Khach_khac_hoac_noi_dung_rong_khong_tra_loi_duoc()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();
        await c.Reply.Handle(new ReplySupportRequestCommand(t.Id, null, SupportRequestStatus.WaitCustomer), Ct);

        Assert.Equal(SupportRequestErrors.Reply, (await c.Answer.Handle(new AnswerSupportRequestCommand(t.Id, "  "), Ct)).Error);
        c.F.CurrentUser.SetupGet(u => u.CustomerId).Returns(999);
        Assert.Equal(SupportRequestErrors.NotFound, (await c.Answer.Handle(new AnswerSupportRequestCommand(t.Id, "Xin chào"), Ct)).Error);
    }

    [Fact]
    public async Task Tai_khoan_con_chi_thay_yeu_cau_minh_gui()
    {
        await using var c = new Ctx();
        await c.CreateAsync(); // tài khoản chính gửi
        c.F.SignInAs(7);
        var mine = await c.CreateAsync();

        Assert.Equal([mine.Id], (await c.GetSupportRequests.Handle(new GetSupportRequestsQuery(), Ct)).Value.Select(t => t.Id));
        c.F.SignInAs(null);
        Assert.Equal(2, (await c.GetSupportRequests.Handle(new GetSupportRequestsQuery(), Ct)).Value.Count);
    }

    [Fact]
    public async Task Admin_khong_gui_gi_hoac_trang_thai_sai_thi_bao_loi()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();
        Assert.Equal(SupportRequestErrors.NothingToSend, (await c.Reply.Handle(new ReplySupportRequestCommand(t.Id, " ", null), Ct)).Error);
        Assert.Equal(SupportRequestErrors.NothingToSend, (await c.Reply.Handle(new ReplySupportRequestCommand(t.Id, null, SupportRequestStatus.New), Ct)).Error);
        Assert.Equal(SupportRequestErrors.Status, (await c.Reply.Handle(new ReplySupportRequestCommand(t.Id, "x", "abc"), Ct)).Error);
    }

    [Fact]
    public async Task Sua_thong_tin_nguoi_gui_la_yeu_cau_ho_tro_tep_chi_gui_kem_email()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync(Support(), new SupportRequestFile(@"C:\fakepath\giay-phep.pdf", "application/pdf", [1, 2, 3]));

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
        Assert.Equal(("gop_y", "Khác"), (t.Kind, t.Type)); // không chọn nội dung → "Khác" như form cũ
        // Như form "Yêu cầu" Bill Online: KeyWord = "Feedback", loại góp ý ở Title.
        var row = await c.F.Db.SupportRequests.SingleAsync(Ct);
        Assert.Equal(("Feedback", "Khác"), (row.Type, row.Title));
        var upgrade = await c.CreateAsync(Feedback() with { Type = "Góp ý nâng cấp" });
        Assert.Equal("Góp ý nâng cấp", upgrade.Type);
        Assert.Equal(2, (await c.GetAdminSupportRequests.Handle(new GetAdminSupportRequestsQuery(), Ct)).Count);
    }

    [Fact]
    public async Task Quan_tri_thay_nhan_vien_phu_trach_loc_theo_nhan_vien_chi_con_khach_cua_ho()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();
        c.F.Customers.Setup(x => x.GetAccountManagersAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, AccountManager> { [IdentityFixture.CustomerId] = new(15, "han.nguyen", "Nguyễn Ngọc Hân", null, null, 1, true) });
        c.F.Customers.Setup(x => x.GetCustomerIdsOfManagerAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync([IdentityFixture.CustomerId]);
        c.F.Customers.Setup(x => x.GetCustomerIdsOfManagerAsync(16, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var all = Assert.Single(await c.GetAdminSupportRequests.Handle(new GetAdminSupportRequestsQuery(), Ct));
        Assert.Equal((t.Id, 15, "Nguyễn Ngọc Hân"), (all.Ticket.Id, all.ManagerId, all.ManagerName));
        Assert.Single(await c.GetAdminSupportRequests.Handle(new GetAdminSupportRequestsQuery(15), Ct));
        Assert.Empty(await c.GetAdminSupportRequests.Handle(new GetAdminSupportRequestsQuery(16), Ct));
    }

    [Theory]
    [InlineData("TRB-12", 12)]
    [InlineData("trb-7", 7)]
    [InlineData("15", 15)]
    [InlineData("TRB-", null)]
    [InlineData("abc", null)]
    public void Doc_ma_yeu_cau(string code, int? expected) => Assert.Equal(expected, SupportRequest.ParseCode(code));

    [Theory]
    [InlineData("new", 0)]
    [InlineData("doing", 1)]
    [InlineData("waitc", 2)]
    [InlineData("done", 3)]
    public void Trang_thai_luu_so_o_TContact(string status, int number)
    {
        Assert.Equal(number, SupportRequestStatus.ToNumber(status));
        Assert.Equal(status, SupportRequestStatus.FromNumber(number));
    }

    [Fact]
    public void FormType_cua_portal()
    {
        Assert.Equal("Portal - Hỗ trợ", SupportRequestKind.FormTypeOf(SupportRequestKind.Support));
        Assert.Equal(SupportRequestKind.Feedback, SupportRequestKind.FromFormType("Portal - Góp ý"));
    }

    [Fact]
    public void Kiem_tra_gui_yeu_cau()
    {
        Assert.Null(SupportRequestRules.Validate(Request()));
        Assert.Equal(SupportRequestErrors.Description, SupportRequestRules.Validate(Request() with { Desc = "ngắn" }));
        Assert.Equal(SupportRequestErrors.Priority, SupportRequestRules.Validate(Request() with { Lv = "urgent" }));
        Assert.Null(SupportRequestRules.Validate(Request(" ")));                      // mã vận đơn không bắt buộc
        Assert.Equal(SupportRequestErrors.Bill, SupportRequestRules.Validate(Request(new string('9', SupportRequest.BillMaxLength + 1))));
        Assert.Equal(SupportRequestErrors.Kind, SupportRequestRules.Validate(Request() with { Kind = "khac" }));
        Assert.Null(SupportRequestRules.Validate(Support()));
        Assert.Null(SupportRequestRules.Validate(Support("")));                       // tiêu đề không bắt buộc
        Assert.Equal(SupportRequestErrors.Subject, SupportRequestRules.Validate(Support(new string('a', SupportRequest.SubjectMaxLength + 1))));
        Assert.Equal(SupportRequestErrors.Contact, SupportRequestRules.Validate(Support() with { Contact = " " }));
        Assert.Null(SupportRequestRules.Validate(Feedback()));
        Assert.Null(SupportRequestRules.Validate(Feedback() with { Type = "Góp ý nâng cấp" }));
        Assert.Equal(SupportRequestErrors.FeedbackTopic, SupportRequestRules.Validate(Feedback() with { Type = "Giao chậm" }));
        Assert.Equal(SupportRequestErrors.FileType, SupportRequestRules.Validate(Support(), new SupportRequestFile("x.exe", "application/octet-stream", [1])));
        Assert.Equal(SupportRequestErrors.FileTooLarge, SupportRequestRules.Validate(Support(), new SupportRequestFile("x.pdf", "application/pdf", new byte[SupportRequest.FileMaxBytes + 1])));
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
