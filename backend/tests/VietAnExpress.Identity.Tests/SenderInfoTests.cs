using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class SenderInfoTests
{
    [Fact]
    public async Task Ho_so_da_doi_thi_API_duyet_cung_tu_choi_ghi_de()
    {
        await using var c = new Ctx();
        var r = await c.RequestAsync();
        c.F.Customers.Setup(x => x.GetByIdAsync(IdentityFixture.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ctx.Profile with { Phone = "0988888888" });
        var result = await c.Admin.Handle(new ApproveSenderInfoRequestCommand(r.Id), Ct);
        Assert.Equal(SenderInfoErrors.Outdated, result.Error);
        Assert.Null(c.Written);
        Assert.Equal("pending", (await c.F.Db.SenderInfoRequests.SingleAsync(Ct)).Status);
        Assert.Empty(await c.F.Db.Notifications.ToListAsync(Ct));
    }

    [Fact]
    public async Task Thay_doi_khac_truong_khach_yeu_cau_van_duyet_va_khong_ghi_de_truong_do()
    {
        await using var c = new Ctx();
        var r = await c.RequestAsync();
        c.F.Customers.Setup(x => x.GetByIdAsync(IdentityFixture.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ctx.Profile with { CompanyName = "Tên đã đổi bởi vận hành" });
        Assert.True((await c.Admin.Handle(new ApproveSenderInfoRequestCommand(r.Id), Ct)).IsSuccess);
        Assert.Null(c.Written!.CompanyName);
        Assert.Equal("0901234567", c.Written.Phone);
    }

    [Fact]
    public async Task Khach_khac_khong_huy_duoc_yeu_cau_va_pending_luon_hien_du_lich_su_dai()
    {
        await using var c = new Ctx();
        var r = await c.RequestAsync();
        for (var i = 0; i < 15; i++)
        {
            var closed = SenderInfoRequest.Create(IdentityFixture.CustomerId, "K", "u", new("A", "B", "0901234567", null, "Địa chỉ cũ", null),
                new("A", "B", "0901234568", null, "Địa chỉ cũ", null), null, new DateTime(2026, 10, 13).AddMinutes(i));
            closed.Cancel(new DateTime(2026, 10, 13).AddMinutes(i));
            c.F.Db.SenderInfoRequests.Add(closed);
        }
        await c.F.Db.SaveChangesAsync(Ct);
        var overview = (await c.Customer.Handle(new GetSenderInfoQuery(), Ct)).Value;
        Assert.Equal(r.Id, overview.Pending!.Id);
        Assert.Equal(10, overview.History.Count);
        c.F.CurrentUser.SetupGet(x => x.CustomerId).Returns(999);
        Assert.Equal(SenderInfoErrors.NotFound, (await c.Customer.Handle(new CancelSenderInfoRequestCommand(r.Id), Ct)).Error);
    }

    [Fact]
    public void Model_bao_ve_mot_pending_va_trang_thai_xu_ly_dong_thoi()
    {
        using var db = new VietAnExpress.Identity.Infrastructure.IdentityDbContext(new DbContextOptionsBuilder<VietAnExpress.Identity.Infrastructure.IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=sender_model;Integrated Security=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var entity = db.Model.FindEntityType(typeof(SenderInfoRequest))!;
        Assert.True(entity.FindProperty(nameof(SenderInfoRequest.Status))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Trang_Thai] = 'pending'");
    }
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Ctx : IAsyncDisposable
    {
        public IdentityFixture F { get; } = new();
        public SenderInfoUpdate? Written { get; private set; }

        public Ctx()
        {
            F.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([Profile]);
            F.Customers.Setup(c => c.UpdateSenderInfoAsync(IdentityFixture.CustomerId, It.IsAny<SenderInfoUpdate>(), It.IsAny<CancellationToken>()))
                .Callback<long, SenderInfoUpdate, CancellationToken>((_, u, _) => Written = u)
                .ReturnsAsync(Profile with { Phone = "0901234567" });
        }

        public static CustomerSummary Profile => new(IdentityFixture.CustomerId, "SaigonbayHN", "SGB EXPRESS HN", "Đức Anh", "ops@sgb.vn", "0977714964", null, null);

        public CustomerSenderInfoHandlers Customer => new(F.Db, F.CurrentUser.Object, F.Customers.Object, F.Jobs, F.Company, F.Clock);
        public AdminSenderInfoHandlers Admin => new(F.Db, F.CurrentUser.Object, F.Customers.Object, F.Jobs, F.Company, F.Clock);
        public CustomerNotificationHandlers Notifications => new(F.Db, F.CurrentUser.Object, F.Clock);

        public async Task<SenderInfoRequestDto> RequestAsync(SenderInfoChangeRequest? r = null)
        {
            var result = await Customer.Handle(new RequestSenderInfoChangeCommand(r ?? Change()), Ct);
            Assert.True(result.IsSuccess, result.Error.Message);
            return result.Value;
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    /// <summary>Giữ nguyên hồ sơ, chỉ đổi SĐT và thêm địa chỉ.</summary>
    private static SenderInfoChangeRequest Change(string phone = "0901234567") =>
        new("SGB EXPRESS HN", "Đức Anh", phone, "ops@sgb.vn", "12 Lý Thường Kiệt, Hà Nội", null, "Đổi số hotline");

    [Fact]
    public async Task Khach_gui_yeu_cau_luu_gia_tri_cu_va_bao_ve_hop_thu()
    {
        await using var c = new Ctx();
        var r = await c.RequestAsync();

        Assert.Equal("pending", r.Status);
        Assert.Equal(["phone", "address"], r.Changed);
        Assert.Equal(("0977714964", "0901234567"), (r.Old.Phone, r.New.Phone));
        Assert.Contains(r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), Assert.Single(c.F.Jobs.Jobs).Name);

        var overview = (await c.Customer.Handle(new GetSenderInfoQuery(), Ct)).Value;
        Assert.Equal(r.Id, overview.Pending?.Id);
        Assert.Equal("0977714964", overview.Current.Phone); // chưa duyệt thì hồ sơ chưa đổi
        c.F.Customers.Verify(x => x.UpdateSenderInfoAsync(It.IsAny<long>(), It.IsAny<SenderInfoUpdate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Moi_luc_chi_1_yeu_cau_cho_duyet_huy_roi_gui_lai_duoc()
    {
        await using var c = new Ctx();
        var first = await c.RequestAsync();

        Assert.Equal(SenderInfoErrors.PendingExists, (await c.Customer.Handle(new RequestSenderInfoChangeCommand(Change("0911111111")), Ct)).Error);
        Assert.Equal("cancelled", (await c.Customer.Handle(new CancelSenderInfoRequestCommand(first.Id), Ct)).Value.Status);
        Assert.Equal(SenderInfoErrors.Closed, (await c.Customer.Handle(new CancelSenderInfoRequestCommand(first.Id), Ct)).Error);
        await c.RequestAsync(Change("0911111111"));
    }

    [Fact]
    public async Task Tai_khoan_con_khong_gui_duoc()
    {
        await using var c = new Ctx();
        c.F.SignInAs(7);
        Assert.Equal("ADMIN_ONLY", (await c.Customer.Handle(new RequestSenderInfoChangeCommand(Change()), Ct)).Error.Code);
        Assert.Equal("ADMIN_ONLY", (await c.Customer.Handle(new GetSenderInfoQuery(), Ct)).Error.Code);
    }

    [Fact]
    public async Task Admin_duyet_chi_ghi_truong_khach_sua_va_bao_khach()
    {
        await using var c = new Ctx();
        var r = await c.RequestAsync();

        var approved = await c.Admin.Handle(new ApproveSenderInfoRequestCommand(r.Id), Ct);

        Assert.True(approved.IsSuccess, approved.Error.Message);
        Assert.Equal("approved", approved.Value.Request.Status);
        Assert.Equal(new SenderInfoUpdate(Phone: "0901234567", Address: "12 Lý Thường Kiệt, Hà Nội"), c.Written);
        var notice = Assert.Single((await c.Notifications.Handle(new GetNotificationsQuery(), Ct)).Value.Data);
        Assert.Equal("Đã duyệt sửa thông tin người gửi", notice.Title);
        Assert.Contains("0977714964 → 0901234567", notice.Body);
        Assert.Equal("https://portal.test/account/sender-info", notice.Link);
        Assert.Contains(c.F.Jobs.Jobs, j => j.Name.Contains("kết quả", StringComparison.Ordinal));

        Assert.Equal(SenderInfoErrors.Closed, (await c.Admin.Handle(new ApproveSenderInfoRequestCommand(r.Id), Ct)).Error);
    }

    [Fact]
    public async Task Admin_tu_choi_phai_co_ly_do_va_khong_ghi_ho_so()
    {
        await using var c = new Ctx();
        var r = await c.RequestAsync();

        Assert.Equal(SenderInfoErrors.Reason, (await c.Admin.Handle(new RejectSenderInfoRequestCommand(r.Id, " "), Ct)).Error);
        var rejected = await c.Admin.Handle(new RejectSenderInfoRequestCommand(r.Id, "Mã số thuế không khớp giấy phép"), Ct);

        Assert.Equal(("rejected", "Mã số thuế không khớp giấy phép"), (rejected.Value.Request.Status, rejected.Value.Request.Reason));
        Assert.Null(c.Written);
        Assert.True(Assert.Single(await c.F.Db.Notifications.ToListAsync(Ct)).Important);
        var history = (await c.Customer.Handle(new GetSenderInfoQuery(), Ct)).Value;
        Assert.Null(history.Pending);
        Assert.Equal("rejected", Assert.Single(history.History).Status);
    }

    [Fact]
    public async Task Admin_thay_ho_so_da_doi_trong_luc_cho_duyet()
    {
        await using var c = new Ctx();
        await c.RequestAsync();
        c.F.Customers.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Ctx.Profile with { Phone = "0988888888" }]);

        var row = Assert.Single((await c.Admin.Handle(new GetAdminSenderInfoRequestsQuery(), Ct)).Value);
        Assert.True(row.Outdated);
        Assert.Equal("0988888888", row.Live?.Phone);
    }

    [Fact]
    public void Kiem_tra_thong_tin_moi()
    {
        var current = new SenderInfo("SGB", "Anh", "0977 714 964", "ops@sgb.vn", "12 Lý Thường Kiệt", "abc"); // MST cũ sai chuẩn
        SenderInfo Next(Func<SenderInfo, SenderInfo> f) => f(current);

        Assert.Equal(SenderInfoErrors.NoChange, SenderInfoRules.Validate(current, current, null));
        Assert.Null(SenderInfoRules.Validate(current, Next(s => s with { Phone = "+84 (90) 123-4567" }), null)); // MST cũ không đổi → không chặn
        Assert.Equal(SenderInfoErrors.Phone, SenderInfoRules.Validate(current, Next(s => s with { Phone = "abc12345" }), null));
        Assert.Equal(SenderInfoErrors.Phone, SenderInfoRules.Validate(current, Next(s => s with { Phone = null }), null));
        Assert.Equal(SenderInfoErrors.Email, SenderInfoRules.Validate(current, Next(s => s with { Email = "ops@" }), null));
        Assert.Null(SenderInfoRules.Validate(current, Next(s => s with { Email = null }), null));
        Assert.Equal(SenderInfoErrors.TaxCode, SenderInfoRules.Validate(current, Next(s => s with { TaxCode = "03123" }), null));
        Assert.Null(SenderInfoRules.Validate(current, Next(s => s with { TaxCode = "0312345678-001" }), null));
        Assert.Equal(SenderInfoErrors.CompanyName, SenderInfoRules.Validate(current, Next(s => s with { CompanyName = "" }), null));
        Assert.Equal(SenderInfoErrors.Address, SenderInfoRules.Validate(current, Next(s => s with { Address = "HN" }), null));
        Assert.Equal(SenderInfoErrors.Note, SenderInfoRules.Validate(current, Next(s => s with { Phone = "0901234567" }), new string('x', 501)));
    }

    [Fact]
    public void Xoa_trang_truong_thi_ghi_chuoi_rong()
    {
        var r = SenderInfoRequest.Create(1, "K", "u",
            new SenderInfo("A", "B", "0901234567", "a@b.vn", "Địa chỉ cũ", "0312345678"),
            new SenderInfo("A", "B", "0901234567", null, "Địa chỉ cũ", "0312345678"), null, DateTime.UnixEpoch);
        Assert.Equal(new SenderInfoUpdate(Email: ""), SenderInfoRules.ToUpdate(r));
    }

    [Fact]
    public void Email_ket_qua_gui_ca_email_cu_va_moi()
    {
        var r = SenderInfoRequest.Create(1, "K", "u",
            new SenderInfo("A", "B", "0901234567", "cu@sgb.vn", "Địa chỉ cũ", null),
            new SenderInfo("A", "B", "0901234567", "moi@sgb.vn", "Địa chỉ cũ", null), null, DateTime.UnixEpoch);
        r.Approve("admin", DateTime.UnixEpoch);
        Assert.Equal(["cu@sgb.vn", "moi@sgb.vn"], SenderInfoRules.CustomerEmail(r, new("Việt An", null, null, null, null))!.To);
    }
}
