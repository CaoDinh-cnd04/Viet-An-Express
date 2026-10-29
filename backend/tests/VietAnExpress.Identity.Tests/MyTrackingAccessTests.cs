using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Shared;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>MyTracking chỉ mở khi CMS bật TCustomer.ViewLinkTracking = 1; tài khoản con không bao giờ vào được.</summary>
public class MyTrackingAccessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task CloseMyTracking(IdentityFixture f)
    {
        var login = await f.Db.Logins.SingleAsync(l => l.CustomerId == IdentityFixture.CustomerId, Ct);
        f.Db.Remove(login);
        await f.Db.SaveChangesAsync(Ct);
        f.Db.Logins.Add(new CustomerLogin(IdentityFixture.CustomerId, IdentityFixture.UserName, IdentityFixture.Password, viewLinkTracking: 0));
        await f.Db.SaveChangesAsync(Ct);
        f.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Cms_chua_mo_thi_tai_khoan_chinh_khong_co_quyen_va_khong_doc_ghi_duoc()
    {
        await using var f = new IdentityFixture();
        await CloseMyTracking(f);
        var login = await f.Db.Logins.AsNoTracking().SingleAsync(l => l.CustomerId == IdentityFixture.CustomerId, Ct);

        var user = await f.Sessions.BuildUserAsync(login, Ct);
        Assert.DoesNotContain(IdentityPermissions.MyTracking, user!.Permissions);
        Assert.Contains(IdentityPermissions.ManageStaff, user.Permissions);

        Assert.Equal(MyTrackingErrors.MyTrackingNotAllowed, (await f.GetMyTracking.Handle(new GetMyTrackingQuery(), Ct)).Error);
        using var config = JsonDocument.Parse("{}");
        Assert.Equal(MyTrackingErrors.MyTrackingNotAllowed, (await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(config.RootElement), Ct)).Error);
        Assert.Empty(await f.Db.MyTrackingPages.ToListAsync(Ct));
    }

    [Fact]
    public async Task Cms_da_mo_thi_tai_khoan_chinh_co_quyen_tai_khoan_con_van_khong()
    {
        await using var f = new IdentityFixture();
        var login = await f.Db.Logins.AsNoTracking().SingleAsync(l => l.CustomerId == IdentityFixture.CustomerId, Ct);
        Assert.Contains(IdentityPermissions.MyTracking, (await f.Sessions.BuildUserAsync(login, Ct))!.Permissions);
        Assert.True((await f.GetMyTracking.Handle(new GetMyTrackingQuery(), Ct)).IsSuccess);

        f.SignInAs(55);
        Assert.Equal(IdentityErrors.AdminOnly, (await f.GetMyTracking.Handle(new GetMyTrackingQuery(), Ct)).Error);
    }
}
