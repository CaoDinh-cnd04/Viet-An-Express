using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

internal sealed record TrackUsageCommand(Guid Id, Guid VisitorId, string Path, string Kind, string? Mode, Guid? FlowId) : IRequest<Result<bool>>;
internal sealed record UsageReportQuery(DateOnly From, DateOnly To, string Audience = "all") : IRequest<Result<UsageReport>>;
internal sealed record UsagePage(string Path, int Views, int Visitors);
internal sealed record UsageMode(string Mode, int Views, int Visitors, int Starts, int Completed, int Switches);
internal sealed record UsageDay(string Date, int Views, int Visitors);
internal sealed record UsageReport(int Views, int Visitors, IReadOnlyList<UsagePage> Pages, IReadOnlyList<UsageMode> Modes, IReadOnlyList<UsageDay> Days);

internal static class UsageRules
{
    internal static readonly HashSet<string> Paths = ["/", "/login", "/tracking", "/t", "/privacy", "/shopify",
        "/orders/new", "/orders/new/quick", "/orders/import", "/orders", "/drafts", "/pricing", "/ecommerce", "/ecommerce/orders",
        "/troubles", "/notifications", "/help", "/account/api-tracking", "/account/mytracking", "/account/staff", "/account/password", "/account/feedback"];
    internal static readonly Error Invalid = Error.Validation("INVALID_USAGE", "Dữ liệu thống kê không hợp lệ");
    internal static readonly Error InvalidRange = Error.Validation("INVALID_RANGE", "Chọn khoảng ngày hợp lệ, tối đa 366 ngày và loại tài khoản hợp lệ");

    internal static bool Valid(TrackUsageCommand c) => c.Id != Guid.Empty && c.VisitorId != Guid.Empty && Paths.Contains(c.Path)
        && (c.Kind == "page_view" ? c.Mode is null && c.FlowId is null
            : c.Kind is "order_start" or "order_complete" or "mode_switch"
                && c.FlowId is { } flow && flow != Guid.Empty
                && ((c.Mode == "wizard" && c.Path == "/orders/new") || (c.Mode == "quick" && c.Path == "/orders/new/quick")));
}

internal sealed class TrackUsageHandler(IdentityDbContext db, ICurrentUser user, TimeProvider clock) : IRequestHandler<TrackUsageCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(TrackUsageCommand c, CancellationToken ct)
    {
        if (!UsageRules.Valid(c)) return UsageRules.Invalid;
        // The admin has CustomerId=0. Never include admin activity or trust a client-supplied audience.
        if (user.IsAuthenticated && user.CustomerId is not > 0) return true;
        if (c.Kind != "page_view" && (!user.IsAuthenticated || user.CustomerId is not > 0)) return UsageRules.Invalid;
        if (!user.IsAuthenticated && c.Path is not ("/" or "/login" or "/tracking" or "/t" or "/privacy" or "/shopify")) return UsageRules.Invalid;
        if (await db.UsageEvents.AnyAsync(x => x.Id == c.Id, ct)) return true;
        var e = new UsageEvent
        {
            Id = c.Id, Path = c.Path, Kind = c.Kind, Mode = c.Mode, FlowId = c.FlowId,
            Visitor = user.IsAuthenticated ? $"c:{user.CustomerId}:s:{user.StaffId ?? 0}" : $"a:{c.VisitorId:N}",
            Audience = user.IsAuthenticated ? (user.StaffId is null ? "customer" : "staff") : "anonymous",
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.UsageEvents.Add(e);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(e).State = EntityState.Detached; // A retry raced the original event.
        }
        return true;
    }
}

internal sealed class UsageReportHandler(IdentityDbContext db) : IRequestHandler<UsageReportQuery, Result<UsageReport>>
{
    public async Task<Result<UsageReport>> Handle(UsageReportQuery q, CancellationToken ct)
    {
        if (q.From.Year < 2000 || q.To < q.From || q.To.DayNumber - q.From.DayNumber >= 366 || q.To == DateOnly.MaxValue
            || q.Audience is not ("all" or "customer" or "staff" or "anonymous")) return UsageRules.InvalidRange;
        // Inclusive calendar dates in Vietnam (UTC+7); SQL stores UTC.
        var start = q.From.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var end = q.To.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var events = db.UsageEvents.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        if (q.Audience != "all") events = events.Where(x => x.Audience == q.Audience);
        var views = events.Where(x => x.Kind == "page_view");
        var pageRows = await views.GroupBy(x => x.Path).Select(g => new { Path = g.Key, Views = g.Count(), Visitors = g.Select(x => x.Visitor).Distinct().Count() })
            .OrderByDescending(x => x.Views).ThenBy(x => x.Path).ToListAsync(ct);
        var pages = pageRows.Select(x => new UsagePage(x.Path, x.Views, x.Visitors)).ToArray();
        var modeViews = pages.Where(x => x.Path is "/orders/new" or "/orders/new/quick").ToDictionary(x => x.Path);
        var actions = await events.Where(x => x.Mode != null).GroupBy(x => x.Mode).Select(g => new
        {
            Mode = g.Key!,
            Starts = g.Where(x => x.Kind == "order_start" || x.Kind == "order_complete").Select(x => x.FlowId).Distinct().Count(),
            Completed = g.Where(x => x.Kind == "order_complete").Select(x => x.FlowId).Distinct().Count(),
            Switches = g.Count(x => x.Kind == "mode_switch")
        }).ToListAsync(ct);
        var modes = new[] { "wizard", "quick" }.Select(mode =>
        {
            modeViews.TryGetValue(mode == "wizard" ? "/orders/new" : "/orders/new/quick", out var p);
            var a = actions.Find(x => x.Mode == mode);
            return new UsageMode(mode, p?.Views ?? 0, p?.Visitors ?? 0, a?.Starts ?? 0, a?.Completed ?? 0, a?.Switches ?? 0);
        }).ToArray();
        var daily = await views.GroupBy(x => x.CreatedAt.AddHours(7).Date)
            .Select(g => new { Date = g.Key, Views = g.Count(), Visitors = g.Select(x => x.Visitor).Distinct().Count() }).OrderBy(x => x.Date).ToListAsync(ct);
        return new UsageReport(await views.CountAsync(ct), await views.Select(x => x.Visitor).Distinct().CountAsync(ct), pages, modes,
            daily.Select(x => new UsageDay(x.Date.ToString("yyyy-MM-dd"), x.Views, x.Visitors)).ToArray());
    }
}
