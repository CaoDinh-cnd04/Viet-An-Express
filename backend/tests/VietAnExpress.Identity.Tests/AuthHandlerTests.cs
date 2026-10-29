using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
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
    private readonly string imageDirectory = Path.Combine(Path.GetTempPath(), "vietan_mytracking_" + Guid.NewGuid().ToString("N"));
    public MyTrackingImageStore Images { get; }
    public NotificationImageStore NotificationImages { get; }
    public Mock<IHostEnvironment> Environment { get; } = new();
    public RevokedTokenStore RevokedTokens { get; }
    public EmailCompany Company { get; } = new("Việt An Express", "14 Sâm Sơn, Tân Bình", "+84 909 805 845", "www.vietanexpress.com.vn", "https://portal.test");

    public IdentityFixture()
    {
        var environment = Environment;
        environment.SetupGet(e => e.ContentRootPath).Returns(imageDirectory);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("portal.test");
        Images = new MyTrackingImageStore(environment.Object, new ConfigurationBuilder().Build(), new HttpContextAccessor { HttpContext = context });
        NotificationImages = new NotificationImageStore(environment.Object, new ConfigurationBuilder().Build());
        Tokens = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Jwt), Clock);
        RevokedTokens = NewRevokedTokenStore();
        LoginGuard = new LoginAttemptGuard(Clock);
        Customers.Setup(c => c.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerSummary(CustomerId, "SaigonbayHN", "SGB EXPRESS HN", "Đức Anh", "ops@sgb.vn", "0977714964", null, null));
        Customers.Setup(c => c.GetByCodeAsync(It.Is<string>(code => string.Equals(code, "SaigonbayHN", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerSummary(CustomerId, "SaigonbayHN", "SGB EXPRESS HN", "Đức Anh", "ops@sgb.vn", "0977714964", null, null));
        Customers.Setup(c => c.GetAccountManagersAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, AccountManager>()); // khách chưa phân công nhân viên
        CurrentUser.SetupGet(u => u.CustomerId).Returns(CustomerId);
        CurrentUser.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true); // tài khoản chính
        Db.Logins.Add(new CustomerLogin(CustomerId, UserName, Password, viewLinkTracking: 1)); // CMS đã mở MyTracking
        Db.Logins.Add(new CustomerLogin(200877, "chua-co-mat-khau", ""));
        Db.SaveChanges();
    }

    public SessionService Sessions => new(Tokens, Customers.Object, [new FakePermissions()]);
    public LoginAttemptGuard LoginGuard { get; }
    public LoginHandler Login => new(Db, Sessions, LoginGuard);
    public RefreshSessionHandler Refresh => new(Db, Tokens, Sessions, RevokedTokens, Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<RefreshSessionHandler>.Instance);
    public LogoutHandler Logout => new(Tokens, RevokedTokens);

    /// <summary>Kho thu hồi mới đọc lại file App_Data — giả lập ứng dụng khởi động lại.</summary>
    public RevokedTokenStore NewRevokedTokenStore() =>
        new(Environment.Object, Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<RevokedTokenStore>.Instance);
    public ChangePasswordHandler ChangePassword => new(Db, Tokens, Sessions, CurrentUser.Object);
    public ListStaffHandler ListStaff => new(Db, Sessions, CurrentUser.Object);
    public ListAssignablePermissionsHandler ListAssignablePermissions => new(Sessions, CurrentUser.Object);
    public CreateStaffHandler CreateStaff => new(Db, Sessions, CurrentUser.Object, Clock);
    public UpdateStaffHandler UpdateStaff => new(Db, Sessions, CurrentUser.Object);
    public ResetStaffPasswordHandler ResetStaffPassword => new(Db, CurrentUser.Object);
    public DeleteStaffHandler DeleteStaff => new(Db, CurrentUser.Object);
    public GetMyTrackingHandler GetMyTracking => new(Db, Customers.Object, CurrentUser.Object, Images);
    public SaveMyTrackingHandler SaveMyTracking => new(Db, Customers.Object, CurrentUser.Object, Images);
    public GetCustomerMyTrackingHandler GetCustomerMyTracking => new(Db, Customers.Object, Images);
    public SaveNotificationHandler SaveNotification => new(Db, CurrentUser.Object, Customers.Object, Email, Jobs, NotificationImages, Clock);
    public TestNotificationEmailHandler TestNotificationEmail => new(CurrentUser.Object, Email, NotificationImages, Clock, PortalConfig);
    public GetAdminNotificationsHandler GetAdminNotifications => new(Db, Customers.Object, Clock);
    public GetNotificationAudienceHandler GetNotificationAudience => new(Db);
    public GetNotificationReadersHandler GetNotificationReaders => new(Db, Customers.Object);
    public RemoveNotificationHandler RemoveNotification => new(Db, Customers.Object, Clock);
    public DeleteNotificationHandler DeleteNotification => new(Db);
    public IConfiguration PortalConfig { get; } = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Company:PortalUrl"] = "https://portal.test/" }).Build();
    public GetNotificationsHandler GetNotifications => new(Db, CurrentUser.Object, Clock);
    public MarkNotificationReadHandler MarkNotificationRead => new(Db, CurrentUser.Object, Clock);
    public MarkAllNotificationsReadHandler MarkAllNotificationsRead => new(Db, CurrentUser.Object, Clock);

    /// <summary>Giả lập request của tài khoản con (null = tài khoản chính / admin).</summary>
    public void SignInAs(long? staffId)
    {
        CurrentUser.SetupGet(u => u.StaffId).Returns(staffId);
        // Tài khoản con không bao giờ có quyền chỉ-admin (account.*).
        CurrentUser.Setup(u => u.HasPermission(It.IsAny<string>())).Returns<string>(p => staffId is null || !IdentityPermissions.AdminOnly.Contains(p));
    }
    public GetSessionHandler Me => new(Db, Sessions, CurrentUser.Object);

    public string StoredPassword() => Db.Logins.AsNoTracking().Single(l => l.CustomerId == CustomerId).Password!;

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        if (Directory.Exists(imageDirectory)) Directory.Delete(imageDirectory, recursive: true);
    }

    private sealed class FakePermissions : IPermissionProvider
    {
        public IEnumerable<PermissionDefinition> GetPermissions() =>
        [
            new("shipments.view", "Xem", SystemRoles.Customer),
            new("shipments.create", "Tạo", SystemRoles.Customer),
            new(IdentityPermissions.ManageStaff, "Quản lý nhân viên", SystemRoles.Customer),
            new(IdentityPermissions.MyTracking, "MyTracking", SystemRoles.Customer),
            new(IdentityPermissions.SupportRequests, "Hỗ trợ & sự cố", SystemRoles.Customer),
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
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
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
        Assert.Equal(AuthErrors.SessionExpired, withAccessToken.Error);
    }

    [Fact]
    public async Task Refresh_token_het_han_bi_tu_choi()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;

        f.Clock.Now = f.Clock.Now.AddHours(f.Jwt.SessionRefreshTokenHours + 1);
        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Error);
    }

    [Fact]
    public async Task Sai_mat_khau_5_lan_thi_khoa_tam_15_phut_ke_ca_mat_khau_dung()
    {
        await using var f = new IdentityFixture();
        for (var i = 0; i < LoginAttemptGuard.MaxFailures; i++)
            Assert.Equal(AuthErrors.InvalidCredentials, (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, "sai", false), Ct)).Error);

        var locked = await f.Login.Handle(new LoginCommand(" " + IdentityFixture.UserName.ToUpperInvariant(), IdentityFixture.Password, false), Ct);
        Assert.Equal("LOGIN_LOCKED", locked.Error.Code);
        Assert.Contains("15 phút", locked.Error.Message);

        f.Clock.Now = f.Clock.Now + LoginAttemptGuard.Lockout;
        Assert.True((await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Dang_nhap_dung_thi_xoa_bo_dem_sai()
    {
        await using var f = new IdentityFixture();
        for (var i = 0; i < LoginAttemptGuard.MaxFailures - 1; i++)
            await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, "sai", false), Ct);
        Assert.True((await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials, (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, "sai", false), Ct)).Error);
        Assert.Null(f.LoginGuard.LockedFor(IdentityFixture.UserName));
    }

    [Fact]
    public async Task Dang_xuat_thu_hoi_refresh_token_ke_ca_sau_khi_khoi_dong_lai()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).Value;
        var other = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).Value;

        Assert.True((await f.Logout.Handle(new LogoutCommand(login.RefreshToken, null, null), Ct)).IsSuccess);

        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Error);
        Assert.True((await f.Refresh.Handle(new RefreshSessionCommand(other.RefreshToken), Ct)).IsSuccess); // thiết bị khác không ảnh hưởng
        Assert.True(f.NewRevokedTokenStore().IsRevoked((await f.Tokens.ReadRefreshTokenAsync(login.RefreshToken))!.Id)); // đọc lại từ file
        // Token rác / hết hạn: đăng xuất vẫn thành công, không ghi gì.
        Assert.True((await f.Logout.Handle(new LogoutCommand("khong-phai-token", null, null), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Dang_xuat_thu_hoi_access_token_toi_khi_het_han()
    {
        await using var f = new IdentityFixture();
        var expires = f.Clock.Now.AddMinutes(5);
        await f.Logout.Handle(new LogoutCommand(null, "jti-1", expires), Ct);

        Assert.True(f.RevokedTokens.IsRevoked("jti-1"));
        Assert.False(f.RevokedTokens.IsRevoked("jti-2"));
        f.Clock.Now = expires.AddSeconds(1);
        Assert.False(f.RevokedTokens.IsRevoked("jti-1")); // token đã hết hạn — tự bị từ chối, không cần nhớ
    }

    [Fact]
    public async Task Phien_tu_gia_han_khong_vuot_qua_gioi_han_tu_lan_nhap_mat_khau()
    {
        await using var f = new IdentityFixture();
        var session = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).Value;
        var loginAt = f.Clock.Now;

        // Vào portal mỗi 10 ngày: vẫn gia hạn được cho tới mốc 30 ngày, hạn token không vượt mốc.
        for (var day = 10; day < f.Jwt.MaxSessionDays; day += 10)
        {
            f.Clock.Now = loginAt.AddDays(day);
            var renewed = await f.Refresh.Handle(new RefreshSessionCommand(session.RefreshToken), Ct);
            Assert.True(renewed.IsSuccess, $"ngày {day}");
            session = renewed.Value;
            Assert.True(session.RefreshTokenExpiresAt <= loginAt.AddDays(f.Jwt.MaxSessionDays));
        }

        f.Clock.Now = loginAt.AddDays(f.Jwt.MaxSessionDays).AddMinutes(1);
        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(session.RefreshToken), Ct)).Error);
        // Đăng nhập lại bằng mật khẩu → mốc mới.
        Assert.True((await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).IsSuccess);
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
        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(other.RefreshToken), Ct)).Error);
        Assert.True((await f.Refresh.Handle(new RefreshSessionCommand(changed.Value.RefreshToken), Ct)).IsSuccess);
        Assert.True((await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, "MatKhauMoi9", false), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Doi_mat_khau_sai_mat_khau_hien_tai_thi_khong_doi()
    {
        await using var f = new IdentityFixture();

        var result = await f.ChangePassword.Handle(new ChangePasswordCommand("sai", "MatKhauMoi9", null), Ct);

        Assert.Equal(AuthErrors.WrongCurrentPassword, result.Error);
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
