using Microsoft.IdentityModel.JsonWebTokens;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>Nhân viên Việt An (dbo.USERS) đăng nhập portal, chọn khách trong phạm vi rồi làm thay khách.</summary>
public class VietAnStaffTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const int AgentId = 7;
    private const string AgentPassword = "246810";
    private const long OtherCustomerId = 200877;

    private static IdentityFixture Fixture(bool viewAll = false, int managerOfCustomer = AgentId)
    {
        var f = new IdentityFixture();
        var agent = new VietAnUserLogin(AgentId, "vo.hanh", "Võ Hạnh", viewAll, AgentPassword, "vo.hanh@vietanexpress.com.vn");
        f.Customers.Setup(c => c.FindVietAnUsersAsync("vo.hanh", It.IsAny<CancellationToken>())).ReturnsAsync([agent]);
        f.Customers.Setup(c => c.GetVietAnUserAsync(AgentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        f.Customers.Setup(c => c.GetAccountManagerAsync(IdentityFixture.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountManager(managerOfCustomer, "x", "X", null, null, null, true));
        // Phạm vi: khách thuộc nhân viên theo Staff_ID / SubTer (CustomerScope) — ở test coi như theo người phụ trách.
        f.Customers.Setup(c => c.IsCustomerOfStaffAsync(IdentityFixture.CustomerId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, int staff, CancellationToken _) => staff == managerOfCustomer);
        f.Customers.Setup(c => c.GetByIdAsync(OtherCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerSummary(OtherCustomerId, "SCS", "SCS CO., LTD", null, null, null, null, null));
        return f;
    }

    private static VietAnOtpDayPass DayPass(IdentityFixture f) => new(f.Tokens, f.Clock);

    private static VietAnLoginHandler Login(IdentityFixture f, bool development = false)
    {
        f.Environment.SetupGet(e => e.EnvironmentName).Returns(development ? Environments.Development : Environments.Production);
        return new(f.Customers.Object, f.LoginGuard, f.VietAnOtps, f.Email, new EmailCompany("Việt An Express", null, null, null, null), f.Environment.Object, NullLogger<VietAnLoginHandler>.Instance, f.Sessions, DayPass(f), f.VietAnSessions);
    }

    private static VerifyVietAnOtpHandler Verify(IdentityFixture f) => new(f.Customers.Object, f.Sessions, f.LoginGuard, f.VietAnOtps, DayPass(f));

    /// <summary>Mã OTP trong email vừa gửi (dòng "...là: 123456").</summary>
    private static string SentCode(IdentityFixture f) => Regex.Match(f.Email.Sent[^1].Body, @"bạn: (\d{6})").Groups[1].Value;

    /// <summary>Đăng nhập đủ 2 bước: mật khẩu → mã OTP trong email.</summary>
    private static async Task<Result<AuthSession>> SignIn(IdentityFixture f, bool remember = false)
    {
        var challenge = (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, remember), Ct)).Value.Challenge!;
        return await Verify(f).Handle(new VerifyVietAnOtpCommand(challenge.ChallengeId, SentCode(f)), Ct);
    }

    /// <summary>Request tiếp theo mang token của phiên nhân viên (đọc claim như HttpCurrentUser).</summary>
    private static ActAsCustomerHandler ActAs(IdentityFixture f, AuthSession session)
    {
        var jwt = new JsonWebToken(session.AccessToken);
        f.CurrentUser.SetupGet(u => u.VietAnUserId).Returns(int.Parse(jwt.GetClaim(VaClaimTypes.VietAnUserId).Value));
        return new ActAsCustomerHandler(f.Customers.Object, f.Sessions, f.Tokens, f.CurrentUser.Object);
    }

    [Fact]
    public async Task Dang_nhap_bang_USERS_chua_gan_khach_chi_co_quyen_chon_khach()
    {
        await using var f = Fixture();
        var result = await SignIn(f);
        Assert.True(result.IsSuccess);
        var user = result.Value.User;
        Assert.Equal((AccountTypes.VietAn, 0L, "Võ Hạnh"), (user.AccountType, user.CustomerId, user.AgentName));
        Assert.Equal([IdentityPermissions.ActForCustomer, IdentityPermissions.ManageSupportRequests, VietAnExpress.Shipments.Contracts.ShipmentsPermissions.ManagedOrders], user.Permissions);
        var jwt = new JsonWebToken(result.Value.AccessToken);
        Assert.Equal("7", jwt.GetClaim(VaClaimTypes.VietAnUserId).Value);
        // Chưa chọn khách → không có claim mã khách, không chạm dữ liệu khách nào.
        Assert.False(jwt.TryGetClaim(VaClaimTypes.CustomerId, out _));
    }

    [Fact]
    public async Task Sai_mat_khau_nhan_vien_bi_tu_choi()
    {
        await using var f = Fixture();
        var result = await Login(f).Handle(new VietAnLoginCommand("vo.hanh", "sai", false), Ct);
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Chon_khach_minh_phu_trach_thi_co_moi_quyen_tai_khoan_chinh_tru_nhom_tai_khoan()
    {
        await using var f = Fixture();
        var login = (await SignIn(f, remember: true)).Value;
        var acted = await ActAs(f, login).Handle(new ActAsCustomerCommand(IdentityFixture.CustomerId, login.RefreshToken), Ct);
        Assert.True(acted.IsSuccess);
        var user = acted.Value.User;
        Assert.Equal((IdentityFixture.CustomerId, "SaigonbayHN", true, "vo.hanh"), (user.CustomerId, user.CustomerCode, user.IsAdmin, user.AgentUserName));
        Assert.Contains("shipments.view", user.Permissions);
        Assert.Contains(IdentityPermissions.ManageSupportRequests, user.Permissions);
        // Nhóm Tài khoản của khách (MyTracking, tài khoản con, đổi mật khẩu) không dành cho nhân viên Việt An.
        Assert.All(IdentityPermissions.AdminOnly, p => Assert.DoesNotContain(p, user.Permissions));
        Assert.True(acted.Value.IsPersistent); // giữ "ghi nhớ" của phiên đăng nhập
        var jwt = new JsonWebToken(acted.Value.AccessToken);
        Assert.Equal((IdentityFixture.CustomerId.ToString(), "7"), (jwt.GetClaim(VaClaimTypes.CustomerId).Value, jwt.GetClaim(VaClaimTypes.VietAnUserId).Value));
        // Mật khẩu khách không đổi.
        Assert.Equal(IdentityFixture.Password, f.StoredPassword());

        // "Về trang quản trị": bỏ khách đang làm → phiên nhân viên chưa gắn khách, không còn tạo đơn / book bill.
        var back = await ActAs(f, acted.Value).Handle(new ActAsCustomerCommand(0, acted.Value.RefreshToken), Ct);
        Assert.True(back.IsSuccess);
        Assert.Equal((0L, ""), (back.Value.User.CustomerId, back.Value.User.CustomerCode));
        Assert.DoesNotContain("shipments.create", back.Value.User.Permissions);
        Assert.Contains("vietan.orders", back.Value.User.Permissions);
        Assert.True(back.Value.IsPersistent);
    }

    [Fact]
    public async Task Khach_khong_do_minh_phu_trach_thi_khong_chon_duoc_tru_khi_xem_moi_khach()
    {
        await using (var f = Fixture(managerOfCustomer: 99))
        {
            var login = (await SignIn(f)).Value;
            var acted = await ActAs(f, login).Handle(new ActAsCustomerCommand(IdentityFixture.CustomerId, login.RefreshToken), Ct);
            Assert.Equal(VietAnStaffErrors.CustomerOutOfScope, acted.Error);
        }
        await using (var f = Fixture(viewAll: true, managerOfCustomer: 99))
        {
            var login = (await SignIn(f)).Value;
            var acted = await ActAs(f, login).Handle(new ActAsCustomerCommand(OtherCustomerId, login.RefreshToken), Ct);
            Assert.True(acted.IsSuccess);
        }
    }

    [Fact]
    public async Task Tai_khoan_khach_khong_goi_duoc_chon_khach()
    {
        await using var f = Fixture();
        var customer = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;
        var handler = new ActAsCustomerHandler(f.Customers.Object, f.Sessions, f.Tokens, f.CurrentUser.Object);
        var acted = await handler.Handle(new ActAsCustomerCommand(IdentityFixture.CustomerId, customer.RefreshToken), Ct);
        Assert.Equal(VietAnStaffErrors.VietAnOnly, acted.Error);
    }

    [Fact]
    public async Task Lam_moi_phien_giu_khach_doi_nguoi_phu_trach_thi_ve_chua_chon_khach_doi_mat_khau_thi_dang_xuat()
    {
        await using var f = Fixture();
        var login = (await SignIn(f)).Value;
        var acted = (await ActAs(f, login).Handle(new ActAsCustomerCommand(IdentityFixture.CustomerId, login.RefreshToken), Ct)).Value;

        var renewed = await f.Refresh.Handle(new RefreshSessionCommand(acted.RefreshToken), Ct);
        Assert.Equal((IdentityFixture.CustomerId, AccountTypes.VietAn), (renewed.Value.User.CustomerId, renewed.Value.User.AccountType));

        f.Customers.Setup(c => c.GetAccountManagerAsync(IdentityFixture.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountManager(99, "khac", "Khác", null, null, null, true));
        f.Customers.Setup(c => c.IsCustomerOfStaffAsync(IdentityFixture.CustomerId, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var reassigned = await f.Refresh.Handle(new RefreshSessionCommand(acted.RefreshToken), Ct);
        Assert.Equal(0L, reassigned.Value.User.CustomerId);

        f.Customers.Setup(c => c.GetVietAnUserAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VietAnUserLogin(AgentId, "vo.hanh", "Võ Hạnh", false, "mat-khau-moi"));
        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(acted.RefreshToken), Ct)).Error);
    }

    [Fact]
    public async Task Dung_mat_khau_chua_vao_duoc_phai_nhap_OTP_gui_email()
    {
        await using var f = Fixture();
        var challenge = await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct);
        Assert.True(challenge.IsSuccess);
        Assert.Equal("vo***@vietanexpress.com.vn", challenge.Value.Challenge!.Email);
        var mail = Assert.Single(f.Email.Sent);
        Assert.Equal(["vo.hanh@vietanexpress.com.vn"], mail.To);

        // Sai mã → từ chối; mã đúng dùng 1 lần.
        Assert.Equal(VietAnStaffErrors.OtpWrong, (await Verify(f).Handle(new VerifyVietAnOtpCommand(challenge.Value.Challenge!.ChallengeId, "000000" == SentCode(f) ? "111111" : "000000"), Ct)).Error);
        Assert.True((await Verify(f).Handle(new VerifyVietAnOtpCommand(challenge.Value.Challenge!.ChallengeId, SentCode(f)), Ct)).IsSuccess);
        Assert.Equal(VietAnStaffErrors.OtpExpired, (await Verify(f).Handle(new VerifyVietAnOtpCommand(challenge.Value.Challenge!.ChallengeId, SentCode(f)), Ct)).Error);
    }

    [Fact]
    public async Task Ma_OTP_het_han_sau_5_phut_va_gui_lai_thi_ma_cu_mat_hieu_luc()
    {
        await using var f = Fixture();
        var first = (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Value.Challenge!;
        var firstCode = SentCode(f);
        var second = (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Value.Challenge!;
        Assert.Equal(VietAnStaffErrors.OtpExpired, (await Verify(f).Handle(new VerifyVietAnOtpCommand(first.ChallengeId, firstCode), Ct)).Error);

        f.Clock.Now += VietAnOtpStore.Lifetime + TimeSpan.FromSeconds(1);
        Assert.Equal(VietAnStaffErrors.OtpExpired, (await Verify(f).Handle(new VerifyVietAnOtpCommand(second.ChallengeId, SentCode(f)), Ct)).Error);
    }

    [Fact]
    public async Task Sai_OTP_5_lan_thi_huy_ma_va_khoa_tam_ten_dang_nhap()
    {
        await using var f = Fixture();
        var challenge = (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Value.Challenge!;
        var wrong = SentCode(f) == "000000" ? "111111" : "000000";
        for (var i = 0; i < VietAnOtpStore.MaxFailures; i++)
            await Verify(f).Handle(new VerifyVietAnOtpCommand(challenge.ChallengeId, wrong), Ct);
        Assert.Equal(VietAnStaffErrors.OtpExpired, (await Verify(f).Handle(new VerifyVietAnOtpCommand(challenge.ChallengeId, SentCode(f)), Ct)).Error);
        Assert.Equal("LOGIN_LOCKED", (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Error.Code);
    }

    [Fact]
    public async Task Nhan_vien_chua_co_email_thi_khong_dang_nhap_duoc_tren_may_chu()
    {
        await using var f = Fixture();
        f.Customers.Setup(c => c.FindVietAnUsersAsync("vo.hanh", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VietAnUserLogin(AgentId, "vo.hanh", "Võ Hạnh", false, AgentPassword, "chua-co")]);
        Assert.Equal(VietAnStaffErrors.NoOtpEmail, (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Error);
        // Dev trên laptop: mã ghi ra log backend, không gửi email.
        Assert.True((await Login(f, development: true).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).IsSuccess);
        Assert.Empty(f.Email.Sent);
    }

    [Fact]
    public void Che_bot_email_va_lay_dia_chi_dau()
    {
        Assert.Equal("th***@gmail.com", VietAnOtp.Mask("thanhtan.vietan@gmail.com"));
        Assert.Equal("a***@x.vn", VietAnOtp.Mask("a@x.vn"));
        Assert.Equal("a@x.vn", VietAnOtp.ValidEmail(" a@x.vn; b@y.vn"));
        Assert.Null(VietAnOtp.ValidEmail("@x"));
    }

    [Fact]
    public void Mat_khau_nhan_vien_so_nhu_phan_mem_cu_md5_hoac_chu_tho()
    {
        var stored = VietAnOtp.LegacyMd5("Hanh2468");
        Assert.Contains('�', VietAnOtp.LegacyMd5("123456")); // byte băm lỗi thành "�" như dữ liệu USERS máy chủ
        Assert.True(VietAnOtp.PasswordMatches(stored, "Hanh2468"));
        Assert.False(VietAnOtp.PasswordMatches(stored, "hanh2468"));
        Assert.True(VietAnOtp.PasswordMatches("123456", "123456"));
        Assert.False(VietAnOtp.PasswordMatches("123456", "12345"));
        Assert.False(VietAnOtp.PasswordMatches("", ""));
    }

    [Fact]
    public async Task Mat_khau_luu_md5_nhu_phan_mem_cu_van_gui_ma_otp()
    {
        var f = Fixture();
        f.Customers.Setup(c => c.FindVietAnUsersAsync("vo.hanh", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VietAnUserLogin(AgentId, "vo.hanh", "Võ Hạnh", false, VietAnOtp.LegacyMd5("Hanh2468"), "vo.hanh@vietanexpress.com.vn")]);
        Assert.True((await Login(f).Handle(new VietAnLoginCommand("vo.hanh", "Hanh2468", false), Ct)).IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials, (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", "hanh246", false), Ct)).Error);
    }

    [Fact]
    public async Task Da_nhap_OTP_hom_nay_thi_dang_xuat_vao_lai_khong_hoi_ma()
    {
        await using var f = Fixture();
        var pass = (await SignIn(f)).Value.OtpDayPass!;
        var sent = f.Email.Sent.Count;

        var again = await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false, pass.Token), Ct);
        Assert.NotNull(again.Value.Session);
        Assert.Null(again.Value.Challenge);
        Assert.Equal(sent, f.Email.Sent.Count);

        // Cookie vẫn phải đi cùng đúng mật khẩu; trình duyệt chưa có cookie thì hỏi mã.
        Assert.Equal(AuthErrors.InvalidCredentials, (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", "sai", false, pass.Token), Ct)).Error);
        Assert.NotNull((await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Value.Challenge);
        Assert.NotNull((await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false, "gia-mao"), Ct)).Value.Challenge);
    }

    [Fact]
    public async Task Cookie_OTP_het_hieu_luc_khi_sang_ngay_moi_gio_Viet_Nam_hoac_doi_mat_khau()
    {
        await using var f = Fixture();
        var agent = new VietAnUserLogin(AgentId, "vo.hanh", "Võ Hạnh", false, AgentPassword, null);
        f.Clock.Now = new DateTimeOffset(2026, 11, 9, 16, 59, 0, TimeSpan.Zero); // 23:59 ngày 09/11 giờ Việt Nam
        var pass = DayPass(f).Issue(agent);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 17, 0, 0, TimeSpan.Zero), pass.ExpiresAt);
        Assert.True(DayPass(f).IsValid(pass.Token, agent));
        Assert.False(DayPass(f).IsValid(pass.Token, agent with { Id = 8 }));
        Assert.False(DayPass(f).IsValid(pass.Token, agent with { Password = "moi" }));

        f.Clock.Now += TimeSpan.FromMinutes(2); // 00:01 ngày 10/11
        Assert.False(DayPass(f).IsValid(pass.Token, agent));
    }

    [Fact]
    public async Task Dang_nhap_may_moi_thi_may_cu_bi_da_ra()
    {
        await using var f = Fixture();
        VerifyVietAnOtpHandler OneSession() => new(f.Customers.Object, f.Sessions, f.LoginGuard, f.VietAnOtps, DayPass(f), f.VietAnSessions);
        async Task<AuthSession> SignInOn()
        {
            var challenge = (await Login(f).Handle(new VietAnLoginCommand("vo.hanh", AgentPassword, false), Ct)).Value.Challenge!;
            return (await OneSession().Handle(new VerifyVietAnOtpCommand(challenge.ChallengeId, SentCode(f)), Ct)).Value;
        }

        var deviceA = await SignInOn();
        Assert.True((await f.Refresh.Handle(new RefreshSessionCommand(deviceA.RefreshToken), Ct)).IsSuccess);

        var deviceB = await SignInOn();
        // Máy A: không làm mới phiên, không chọn khách được nữa; máy B dùng bình thường.
        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(deviceA.RefreshToken), Ct)).Error);
        Assert.Equal(AuthErrors.SessionExpired,
            (await ActAs(f, deviceA).Handle(new ActAsCustomerCommand(IdentityFixture.CustomerId, deviceA.RefreshToken), Ct)).Error);
        Assert.True((await f.Refresh.Handle(new RefreshSessionCommand(deviceB.RefreshToken), Ct)).IsSuccess);
        Assert.True((await ActAs(f, deviceB).Handle(new ActAsCustomerCommand(IdentityFixture.CustomerId, deviceB.RefreshToken), Ct)).IsSuccess);
    }
}
