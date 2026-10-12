using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.SharedKernel.Authorization;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class LogoutTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string SessionIdOf(string token) => new JsonWebToken(token).GetClaim(VaClaimTypes.SessionId).Value;

    [Fact]
    public async Task Lam_moi_phien_giu_nguyen_ma_phien()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;
        var refreshed = (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Value;

        Assert.Equal(SessionIdOf(login.AccessToken), SessionIdOf(login.RefreshToken));
        Assert.Equal(SessionIdOf(login.RefreshToken), SessionIdOf(refreshed.RefreshToken));
        Assert.Equal(SessionIdOf(login.AccessToken), SessionIdOf(refreshed.AccessToken));
    }

    [Fact]
    public async Task Dang_xuat_thu_hoi_phien_ca_refresh_token_cu_va_token_ve_muon()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;
        // Request làm mới phiên chạy đua với đăng xuất: response về muộn vẫn ghi cookie mới cho trình duyệt.
        var raced = (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Value;

        Assert.True(await f.Logout.Handle(new LogoutCommand(login.RefreshToken, null), Ct));

        Assert.Equal(IdentityErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Error);
        Assert.Equal(IdentityErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(raced.RefreshToken), Ct)).Error);
        Assert.True(await f.Revocations.IsRevokedAsync(SessionIdOf(raced.AccessToken), Ct)); // access token còn hạn cũng bị chặn
        Assert.Single(await f.Db.RevokedSessions.ToListAsync(Ct));
    }

    [Fact]
    public async Task Thu_hoi_van_hieu_luc_sau_khi_khoi_dong_lai_va_khong_anh_huong_thiet_bi_khac()
    {
        await using var f = new IdentityFixture();
        var phone = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).Value;
        var laptop = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, true), Ct)).Value;

        await f.Logout.Handle(new LogoutCommand(phone.RefreshToken, null), Ct);
        var restarted = f.NewRevocations();

        Assert.True(await restarted.IsRevokedAsync(SessionIdOf(phone.RefreshToken), Ct));
        Assert.False(await restarted.IsRevokedAsync(SessionIdOf(laptop.RefreshToken), Ct));
        Assert.True((await f.Refresh.Handle(new RefreshSessionCommand(laptop.RefreshToken), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Khong_co_refresh_cookie_thi_thu_hoi_theo_ma_phien_cua_access_token()
    {
        await using var f = new IdentityFixture();
        var login = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value;

        Assert.True(await f.Logout.Handle(new LogoutCommand(null, SessionIdOf(login.AccessToken)), Ct));
        Assert.Equal(IdentityErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(login.RefreshToken), Ct)).Error);
        Assert.False(await f.Logout.Handle(new LogoutCommand(null, null), Ct));
    }
}
