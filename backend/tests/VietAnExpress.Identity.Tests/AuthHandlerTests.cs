using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Authorization;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>Tài khoản khách giả lập bảng dbo.TCustomer (DB in-memory) + đồng hồ chỉnh được.</summary>
internal sealed class IdentityFixture : IAsyncDisposable
{
    public const long CustomerId = 201008;
    public const string UserName = "0309142955-004";
    public const string Password = "123456"; // mật khẩu cũ dạng chữ thường, 6 ký tự như dữ liệu thật

    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 9, 29, 3, 0, 0, TimeSpan.Zero));
    public IdentityDbContext Db { get; } = new(
        new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public JwtOptions Jwt { get; } = new() { Secret = new string('k', 48) };
    public JwtTokenService Tokens { get; }
    public Mock<ICustomersApi> Customers { get; } = new();
    public Mock<ICurrentUser> CurrentUser { get; } = new();
    public RecordingJobQueue Jobs { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public EmailCompany Company { get; } = new("Việt An Express", "14 Sâm Sơn, Tân Bình", "+84 909 805 845", "www.vietanexpress.com.vn", "https://portal.test");

    public IdentityFixture()
    {
        Tokens = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Jwt), Clock);
        Customers.Setup(c => c.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerSummary(CustomerId, "SaigonbayHN", "SGB EXPRESS HN", "Đức Anh", "ops@sgb.vn", "0977714964", null, null));
        CurrentUser.SetupGet(u => u.CustomerId).Returns(CustomerId);
        CurrentUser.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true); // tài khoản chính
        Db.Logins.Add(new CustomerLogin(CustomerId, UserName, Password));
        Db.Logins.Add(new CustomerLogin(200877, "chua-co-mat-khau", ""));
        Db.SaveChanges();
    }

    public SessionService Sessions => new(Tokens, Customers.Object, [new FakePermissions()]);
    public LoginHandler Login => new(Db, Sessions);
    public RefreshSessionHandler Refresh => new(Db, Tokens, Sessions, Microsoft.Extensions.Logging.Abstractions.NullLogger<RefreshSessionHandler>.Instance);
    public ChangePasswordHandler ChangePassword => new(Db, Tokens, Sessions, CurrentUser.Object);
    public StaffHandlers Staff => new(Db, Sessions, CurrentUser.Object, Clock);
    public MyTrackingHandlers MyTracking => new(Db, Customers.Object, CurrentUser.Object, Clock);

    /// <summary>Giả lập request của tài khoản con (null = tài khoản chính / admin).</summary>
    public void SignInAs(long? staffId)
    {
        CurrentUser.SetupGet(u => u.StaffId).Returns(staffId);
        // Tài khoản con không bao giờ có quyền chỉ-admin (account.*).
        CurrentUser.Setup(u => u.HasPermission(It.IsAny<string>())).Returns<string>(p => staffId is null || !IdentityPermissions.AdminOnly.Contains(p));
    }
    public GetSessionHandler Me => new(Db, Sessions, CurrentUser.Object);

    public string StoredPassword() => Db.Logins.AsNoTracking().Single(l => l.CustomerId == CustomerId).Password!;

    public ValueTask DisposeAsync() => Db.DisposeAsync();

    private sealed class FakePermissions : IPermissionProvider
    {
        public IEnumerable<PermissionDefinition> GetPermissions() =>
        [
            new("shipments.view", "Xem", SystemRoles.Customer),
            new("shipments.create", "Tạo", SystemRoles.Customer),
            new(IdentityPermissions.ManageStaff, "Quản lý nhân viên", SystemRoles.Customer),
            new(IdentityPermissions.MyTracking, "MyTracking", SystemRoles.Customer),
            new(IdentityPermissions.Troubles, "Hỗ trợ & sự cố", SystemRoles.Customer),
            new("internal.only", "Không cấp cho khách")
        ];
    }
}

internal sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public class AuthHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dang_nhap_bang_tai_khoan_TCustomer_tra_token_co_ma_khach_va_quyen_khach()
    {
        await using var f = new IdentityFixture();

        var result = await f.Login.Handle(new LoginCommand($" {IdentityFixture.UserName} ", IdentityFixture.Password, Remember: true), Ct);

        Assert.True(result.IsSuccess);
        var s = result.Value;
        Assert.Equal(("SaigonbayHN", "SGB EXPRESS HN", "Đức Anh"), (s.User.CustomerCode, s.User.CompanyName, s.User.ContactName));
        Assert.Equal(["account.mytracking", "account.staff", "account.troubles", "shipments.create", "shipments.view"], s.User.Permissions);
        Assert.True(s.User.IsAdmin);
        Assert.True(s.IsPersistent);

        var jwt = new JsonWebToken(s.AccessToken);
        Assert.Equal("201008", jwt.GetClaim(VaClaimTypes.CustomerId).Value);
        Assert.Contains(jwt.Claims, c => c.Type == VaClaimTypes.Permission && c.Value == "shipments.view");
        Assert.DoesNotContain(jwt.Claims, c => c.Value == "internal.only");
        Assert.DoesNotContain(IdentityFixture.Password, s.RefreshToken); // refresh token chỉ mang dấu HMAC, không mang mật khẩu
    }

    [Theory]
    [InlineData(IdentityFixture.UserName, "sai-mat-khau")]
    [InlineData("khong-ton-tai", IdentityFixture.Password)]
    [InlineData("chua-co-mat-khau", "")]
    public async Task Sai_ten_mat_khau_hoac_tai_khoan_chua_co_mat_khau_thi_tu_choi(string userName, string password)
    {
        await using var f = new IdentityFixture();
        var result = await f.Login.Handle(new LoginCommand(userName, password, false), Ct);
        Assert.Equal(IdentityErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Lam_moi_phien_giu_kieu_ghi_nho_va_tu_choi_access_token()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;

        f.Clock.Now = f.Clock.Now.AddHours(1);
        var refreshed = await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct);
        Assert.True(refreshed.IsSuccess);
        Assert.False(refreshed.Value.IsPersistent);

        var withAccessToken = await f.Refresh.Handle(new RefreshSessionCommand(login.AccessToken), Ct);
        Assert.Equal(IdentityErrors.SessionExpired, withAccessToken.Error);
    }

    [Fact]
    public async Task Refresh_token_het_han_bi_tu_choi()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;

        f.Clock.Now = f.Clock.Now.AddHours(f.Jwt.SessionRefreshTokenHours + 1);
        Assert.Equal(IdentityErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Error);
    }

    [Fact]
    public async Task Doi_mat_khau_ghi_TCustomer_dang_chu_thuong_va_huy_moi_phien_cu()
    {
        await using var f = new IdentityFixture();
        var other = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).Value;

        var changed = await f.ChangePassword.Handle(new ChangePasswordCommand(IdentityFixture.Password, "MatKhauMoi9", other.RefreshToken), Ct);

        Assert.True(changed.IsSuccess);
        Assert.Equal("MatKhauMoi9", f.StoredPassword());      // hệ thống cũ đọc được
        Assert.True(changed.Value.IsPersistent);                // giữ kiểu phiên đang dùng
        Assert.Equal(IdentityErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(other.RefreshToken), Ct)).Error);
        Assert.True((await f.Refresh.Handle(new RefreshSessionCommand(changed.Value.RefreshToken), Ct)).IsSuccess);
        Assert.True((await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, "MatKhauMoi9", false), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Doi_mat_khau_sai_mat_khau_hien_tai_thi_khong_doi()
    {
        await using var f = new IdentityFixture();

        var result = await f.ChangePassword.Handle(new ChangePasswordCommand("sai", "MatKhauMoi9", null), Ct);

        Assert.Equal(IdentityErrors.WrongCurrentPassword, result.Error);
        Assert.Equal(IdentityFixture.Password, f.StoredPassword());
    }

    [Fact]
    public async Task Me_tra_ho_so_khach_dang_dang_nhap()
    {
        await using var f = new IdentityFixture();

        var me = await f.Me.Handle(new GetSessionQuery(), Ct);

        Assert.True(me.IsSuccess);
        Assert.Equal((IdentityFixture.CustomerId, IdentityFixture.UserName, "ops@sgb.vn"), (me.Value.CustomerId, me.Value.UserName, me.Value.Email));
    }
}
