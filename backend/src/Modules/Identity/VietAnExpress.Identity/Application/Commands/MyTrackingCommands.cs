using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application.Dtos;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

// Trang MyTracking của khách — admin cấu hình & xuất bản; ai có link /t/{slug} cũng xem được khi đã xuất bản.

internal sealed record GetMyTrackingQuery : IRequest<Result<MyTrackingDto>>;

/// <param name="Config">Cấu hình dạng JSON object như frontend gửi (backend chỉ kiểm tra là object và ≤ 2 MB).</param>
internal sealed record SaveMyTrackingCommand(string Slug, bool Published, JsonElement Config) : IRequest<Result<MyTrackingDto>>;

internal sealed record GetPublicMyTrackingQuery(string Slug) : IRequest<Result<PublicMyTrackingDto>>;

internal sealed class MyTrackingHandlers(IdentityDbContext db, ICustomersApi customers, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<GetMyTrackingQuery, Result<MyTrackingDto>>,
    IRequestHandler<SaveMyTrackingCommand, Result<MyTrackingDto>>,
    IRequestHandler<GetPublicMyTrackingQuery, Result<PublicMyTrackingDto>>
{
    public async Task<Result<MyTrackingDto>> Handle(GetMyTrackingQuery q, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners).FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);
        if (!string.IsNullOrEmpty(page?.Slug)) return ToDto(page);

        // Chưa cấu hình: gợi ý đường dẫn theo mã khách, frontend dùng cấu hình mặc định.
        var customer = await customers.GetByIdAsync(customerId, ct);
        var slug = MyTrackingPage.SuggestSlug(customer?.Code ?? customerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new MyTrackingDto(slug, false, page is null ? null : new RawJson(page.ConfigJson), page?.UpdatedAt);
    }

    public async Task<Result<MyTrackingDto>> Handle(SaveMyTrackingCommand cmd, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        if (cmd.Config.ValueKind != JsonValueKind.Object) return IdentityErrors.InvalidTrackingConfig;
        var json = cmd.Config.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MyTrackingPage.ConfigMaxBytes) return IdentityErrors.InvalidTrackingConfig;

        var slug = cmd.Slug.Trim().ToLowerInvariant();
        // 1 truy vấn: trang của khách này + trang (nếu có) đang giữ đường dẫn muốn dùng — tối đa 2 dòng.
        var rows = await db.MyTrackingPages.Include(p => p.Banners).Where(p => p.CustomerId == customerId || p.Slug == slug).ToListAsync(ct);
        if (rows.Any(p => p.Slug == slug && p.CustomerId != customerId)) return IdentityErrors.SlugTaken;

        var now = VietnamTime.Now(clock);
        var page = rows.FirstOrDefault(p => p.CustomerId == customerId);
        if (page is null)
            db.MyTrackingPages.Add(page = new MyTrackingPage(customerId, slug, json, cmd.Published, now));
        else
        {
            // Khóa ngoại cũ cho NULL: xóa rõ các ảnh bị thay thế, tránh để lại dòng mồ côi.
            db.MyTrackingBanners.RemoveRange(page.Banners);
            page.Update(slug, json, cmd.Published, now);
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbConcurrency.IsUniqueViolation(ex))
        {
            // Khách khác vừa lấy cùng đường dẫn, hoặc chính khách này lưu lần đầu ở 2 tab cùng lúc.
            db.ChangeTracker.Clear();
            return await db.MyTrackingPages.AnyAsync(p => p.Slug == slug && p.CustomerId != customerId, ct)
                ? IdentityErrors.SlugTaken
                : IdentityErrors.MyTrackingChanged;
        }
        return ToDto(page);
    }

    public async Task<Result<PublicMyTrackingDto>> Handle(GetPublicMyTrackingQuery q, CancellationToken ct)
    {
        var slug = q.Slug.Trim().ToLowerInvariant();
        if (!MyTrackingPage.IsValidSlug(slug)) return IdentityErrors.MyTrackingNotFound;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners).FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        return page is null ? IdentityErrors.MyTrackingNotFound : new PublicMyTrackingDto(page.Slug!, new RawJson(page.ConfigJson));
    }

    private static MyTrackingDto ToDto(MyTrackingPage p) => new(p.Slug!, p.IsPublished, new RawJson(p.ConfigJson), p.UpdatedAt);
}
