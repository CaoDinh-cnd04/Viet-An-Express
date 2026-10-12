using Microsoft.EntityFrameworkCore;
using Moq;
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

        public Ctx() =>
            Shipments.Setup(s => s.FindBillAsync(IdentityFixture.CustomerId, "VAHCM6003585US", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BillSummary("VAHCM6003585US", "John Smith", "United States"));

        public CustomerTroubleHandlers Customer => new(F.Db, F.CurrentUser.Object, F.Customers.Object, Shipments.Object, F.Jobs, F.Company, F.Clock);
        public AdminTroubleHandlers Admin => new(F.Db, F.CurrentUser.Object, F.Company, F.Clock);
        public CustomerNotificationHandlers Notifications => new(F.Db, F.CurrentUser.Object, F.Clock);

        public async Task<TroubleDto> CreateAsync()
        {
            var r = await Customer.Handle(new CreateTroubleCommand(Request()), Ct);
            Assert.True(r.IsSuccess, r.Error.Message);
            return r.Value;
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    private static NewTroubleRequest Request(string bill = "VAHCM6003585US") =>
        new(bill, null, null, "Giao chậm / trễ hẹn", "mid", "Đơn đi Mỹ quá 10 ngày chưa giao.", "Lan", "lan@sgb.vn");

    [Fact]
    public async Task Khach_bao_su_co_tu_dien_nguoi_nhan_va_bao_ve_hop_thu()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();

        Assert.StartsWith("TRB-", t.Id);
        Assert.Equal(("VAHCM6003585US", "John Smith", "United States", "new"), (t.Bill, t.Cnee, t.Ct, t.Status));
        Assert.Contains(t.Id, Assert.Single(c.F.Jobs.Jobs).Name);
        Assert.Equal(TroubleErrors.BillNotFound, (await c.Customer.Handle(new CreateTroubleCommand(Request("VAKHAC")), Ct)).Error);
    }

    [Fact]
    public async Task Admin_tra_loi_doi_trang_thai_thi_rieng_khach_do_nhan_thong_bao()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();

        var replied = await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, "Đã liên hệ hãng, dự kiến giao 15/10.", TroubleStatus.Doing), Ct);

        Assert.True(replied.IsSuccess, replied.Error.Message);
        Assert.Equal("doing", replied.Value.Ticket.Status);
        Assert.Equal("Đã liên hệ hãng, dự kiến giao 15/10.", replied.Value.Ticket.Reply);
        var notice = Assert.Single((await c.Notifications.Handle(new GetNotificationsQuery(), Ct)).Value.Data);
        Assert.Equal($"Sự cố {t.Id}: Đang xử lý", notice.Title);
        Assert.Contains("dự kiến giao 15/10", notice.Body);
        Assert.Equal("https://portal.test/troubles", notice.Link);

        // Khách khác không thấy; trang quản trị Thông báo không lẫn thông báo riêng.
        c.F.CurrentUser.SetupGet(u => u.CustomerId).Returns(999);
        Assert.Empty((await c.Notifications.Handle(new GetNotificationsQuery(), Ct)).Value.Data);
        Assert.Empty(await new AdminNotificationHandlers(c.F.Db, c.F.CurrentUser.Object, c.F.Email, c.F.Jobs, c.F.Clock)
            .Handle(new GetAdminNotificationsQuery(), Ct));
    }

    [Fact]
    public async Task Khach_tra_loi_khi_cho_phan_hoi_thi_quay_lai_dang_xu_ly_dong_roi_khong_gui_them()
    {
        await using var c = new Ctx();
        var t = await c.CreateAsync();
        await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, "Vui lòng gửi ảnh kiện hàng.", TroubleStatus.WaitCustomer), Ct);

        var reply = await c.Customer.Handle(new ReplyTroubleCommand(t.Id, "Đã gửi ảnh qua email.", null), Ct);
        Assert.Equal("doing", reply.Value.Status);
        Assert.Equal(["vietan", "customer"], reply.Value.Messages.Select(m => m.From));
        Assert.Equal(["waitc", "doing"], reply.Value.Messages.Select(m => m.Status));

        var closed = await c.Customer.Handle(new ReplyTroubleCommand(t.Id, "Đã nhận hàng.", TroubleStatus.Done), Ct);
        Assert.Equal("done", closed.Value.Status);
        Assert.Equal(TroubleErrors.Closed, (await c.Customer.Handle(new ReplyTroubleCommand(t.Id, "Thêm", null), Ct)).Error);
        Assert.Equal(TroubleErrors.Status, (await c.Customer.Handle(new ReplyTroubleCommand(t.Id, "x", TroubleStatus.Doing), Ct)).Error);
    }

    [Fact]
    public async Task Tai_khoan_con_chi_thay_su_co_minh_bao()
    {
        await using var c = new Ctx();
        await c.CreateAsync(); // tài khoản chính báo
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
        Assert.Equal(TroubleErrors.Status, (await c.Admin.Handle(new AdminReplyTroubleCommand(t.Id, "x", "abc"), Ct)).Error);
        Assert.Empty(await c.F.Db.Notifications.ToListAsync(Ct));
    }

    [Theory]
    [InlineData("TRB-12", 12L)]
    [InlineData("trb-7", 7L)]
    [InlineData("15", 15L)]
    [InlineData("TRB-", null)]
    [InlineData("abc", null)]
    public void Doc_ma_su_co(string code, long? expected) => Assert.Equal(expected, Trouble.ParseCode(code));

    [Fact]
    public void Kiem_tra_bao_su_co()
    {
        Assert.Null(TroubleRules.Validate(Request()));
        Assert.Equal(TroubleErrors.Description, TroubleRules.Validate(Request() with { Desc = "ngắn" }));
        Assert.Equal(TroubleErrors.Priority, TroubleRules.Validate(Request() with { Lv = "urgent" }));
        Assert.Equal(TroubleErrors.Bill, TroubleRules.Validate(Request(" ")));
    }
}
