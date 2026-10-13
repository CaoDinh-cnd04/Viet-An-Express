using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Api;
using VietAnExpress.SharedKernel.Authorization;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class UsageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static TrackUsageCommand View(string path = "/orders/new") => new(Guid.NewGuid(), Guid.NewGuid(), path, "page_view", null, null);

    [Fact]
    public async Task Retry_khong_dem_trung_va_danh_tinh_lay_tu_phien()
    {
        await using var f = new IdentityFixture();
        f.CurrentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        var handler = new TrackUsageHandler(f.Db, f.CurrentUser.Object, f.Clock);
        var c = View();
        Assert.True((await handler.Handle(c, Ct)).IsSuccess);
        Assert.True((await handler.Handle(c, Ct)).IsSuccess);
        var saved = Assert.Single(await f.Db.UsageEvents.ToListAsync(Ct));
        Assert.Equal($"c:{IdentityFixture.CustomerId}:s:0", saved.Visitor);
        Assert.Equal("customer", saved.Audience);
        f.SignInAs(123);
        await handler.Handle(View(), Ct);
        Assert.Contains(await f.Db.UsageEvents.ToListAsync(Ct), x => x.Audience == "staff" && x.Visitor.EndsWith(":123"));
    }

    [Fact]
    public async Task Khong_luu_query_ma_bill_admin_va_su_kien_form_anonymous()
    {
        await using var f = new IdentityFixture();
        f.CurrentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        var handler = new TrackUsageHandler(f.Db, f.CurrentUser.Object, f.Clock);
        Assert.True((await handler.Handle(View("/tracking/SECRET"), Ct)).IsFailure);
        Assert.True((await handler.Handle(View("/orders/new?draft=123"), Ct)).IsFailure);
        f.CurrentUser.SetupGet(u => u.CustomerId).Returns(0);
        await handler.Handle(View(), Ct);
        Assert.Empty(await f.Db.UsageEvents.ToListAsync(Ct));
        f.CurrentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        Assert.True((await handler.Handle(View(), Ct)).IsFailure);
        Assert.True((await handler.Handle(new(Guid.NewGuid(), Guid.NewGuid(), "/orders/new", "order_complete", "wizard", Guid.NewGuid()), Ct)).IsFailure);
        await handler.Handle(View("/tracking"), Ct);
        Assert.Equal("anonymous", Assert.Single(await f.Db.UsageEvents.ToListAsync(Ct)).Audience);
    }

    [Fact]
    public async Task Bao_cao_dem_nguoi_rieng_biet_flow_va_ngay_Viet_Nam()
    {
        await using var f = new IdentityFixture();
        var at = new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc); // midnight Oct 13 in Vietnam
        var flow = Guid.NewGuid();
        void Add(string visitor, string kind, string? mode, DateTime time, string audience = "customer") => f.Db.UsageEvents.Add(new UsageEvent
        { Id = Guid.NewGuid(), Visitor = visitor, Audience = audience, Path = "/orders/new", Kind = kind, Mode = mode, FlowId = mode is null ? null : flow, CreatedAt = time });
        Add("A", "page_view", null, at.AddSeconds(-1)); // outside range
        Add("A", "page_view", null, at);
        Add("A", "page_view", null, at.AddHours(1));
        Add("B", "page_view", null, at.AddHours(2), "staff");
        Add("A", "order_start", "wizard", at);
        Add("A", "order_start", "wizard", at); // repeated browser event
        Add("A", "order_complete", "wizard", at);
        Add("A", "order_complete", "wizard", at);
        Add("A", "page_view", null, at.AddDays(1)); // end exclusive
        await f.Db.SaveChangesAsync(Ct);
        var handler = new UsageReportHandler(f.Db);
        var q = new UsageReportQuery(new(2026, 10, 13), new(2026, 10, 13));
        var all = (await handler.Handle(q, Ct)).Value;
        Assert.Equal(3, all.Views);
        Assert.Equal(2, all.Visitors);
        Assert.Equal("2026-10-13", Assert.Single(all.Days).Date);
        var wizard = Assert.Single(all.Modes, m => m.Mode == "wizard");
        Assert.Equal(1, wizard.Starts);
        Assert.Equal(1, wizard.Completed);
        var customer = (await handler.Handle(q with { Audience = "customer" }, Ct)).Value;
        Assert.Equal(2, customer.Views);
        Assert.Equal(1, customer.Visitors);
        Assert.True((await handler.Handle(q with { To = new(2027, 11, 1) }, Ct)).IsFailure);
        Assert.True((await handler.Handle(q with { Audience = "invalid" }, Ct)).IsFailure);
    }

    [Fact]
    public void Bao_cao_chi_co_quyen_admin_va_migration_khop_model()
    {
        var attribute = (HasPermissionAttribute)Attribute.GetCustomAttribute(typeof(UsageController).GetMethod("Report")!, typeof(HasPermissionAttribute))!;
        Assert.Contains(IdentityPermissions.AdminAnalytics, attribute.Policy!);
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=usage_model_check;Integrated Security=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        // Translation checks do not need a live SQL Server.
        Assert.Contains("COUNT", db.UsageEvents.GroupBy(x => x.Path).Select(g => new { Views = g.Count(), Visitors = g.Select(x => x.Visitor).Distinct().Count() }).ToQueryString());
        Assert.Contains("DATEADD", db.UsageEvents.GroupBy(x => x.CreatedAt.AddHours(7).Date).Select(g => new { Day = g.Key, Views = g.Count() }).ToQueryString());
    }
}
