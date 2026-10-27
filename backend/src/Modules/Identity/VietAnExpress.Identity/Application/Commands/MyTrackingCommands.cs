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

// Trang MyTracking của khách — admin cấu hình; khách có dòng cấu hình thì ai có link /tracking/{mã khách}/{số bill}?ref=... cũng xem được.

internal sealed record GetMyTrackingQuery : IRequest<Result<MyTrackingDto>>;

/// <param name="Config">Cấu hình dạng JSON object như frontend gửi (backend chỉ kiểm tra là object và ≤ 2 MB).</param>
internal sealed record SaveMyTrackingCommand(JsonElement Config) : IRequest<Result<MyTrackingDto>>;

/// <summary>Trang theo mã khách (TCustomer.CustomerCode) — link /tracking/{mã khách}/....</summary>
internal sealed record GetCustomerMyTrackingQuery(string CustomerCode) : IRequest<Result<PublicMyTrackingDto>>;

internal sealed class MyTrackingHandlers(IdentityDbContext db, ICustomersApi customers, ICurrentUser user, MyTrackingImageStore images) :
    IRequestHandler<GetMyTrackingQuery, Result<MyTrackingDto>>,
    IRequestHandler<SaveMyTrackingCommand, Result<MyTrackingDto>>,
    IRequestHandler<GetCustomerMyTrackingQuery, Result<PublicMyTrackingDto>>
{
    /// <summary>CMS cho phép (TCustomer.ViewLinkTracking = 1) — kiểm tra lại ở đây để CMS tắt là chặn ngay, không đợi token hết hạn.</summary>
    private Task<bool> AllowedAsync(long customerId, CancellationToken ct) =>
        db.Logins.AsNoTracking().AnyAsync(l => l.CustomerId == customerId && l.ViewLinkTracking == 1, ct);

    public async Task<Result<MyTrackingDto>> Handle(GetMyTrackingQuery q, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        if (!await AllowedAsync(customerId, ct)) return IdentityErrors.MyTrackingNotAllowed;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners).FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);
        var customer = await customers.GetByIdAsync(customerId, ct);
        // Chưa cấu hình: config null — frontend dùng cấu hình mặc định.
        return page is null
            ? new MyTrackingDto(null, CustomerCode(customerId, customer), customer?.TrackingName ?? "")
            : ToDto(page, customerId, customer);
    }

    public async Task<Result<MyTrackingDto>> Handle(SaveMyTrackingCommand cmd, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        if (!await AllowedAsync(customerId, ct)) return IdentityErrors.MyTrackingNotAllowed;
        if (cmd.Config.ValueKind != JsonValueKind.Object) return IdentityErrors.InvalidTrackingConfig;
        var json = cmd.Config.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MyTrackingPage.ConfigMaxBytes) return IdentityErrors.InvalidTrackingConfig;
        // Tên công ty không do khách sửa: luôn lưu theo hồ sơ khách.
        var customer = await customers.GetByIdAsync(customerId, ct);
        json = MyTrackingJson.WithCompanyName(json, customer?.TrackingName);
        try { json = await images.PrepareAsync(json, ct); }
        catch (FormatException) { return IdentityErrors.InvalidTrackingConfig; }
        if (MyTrackingChannels.HasTooLongValue(json)) return IdentityErrors.InvalidTrackingConfig;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(() => SaveAsync(customerId, customer, json, ct));
    }

    private async Task<Result<MyTrackingDto>> SaveAsync(long customerId, CustomerSummary? customer, string json, CancellationToken ct)
    {
        // Bảng gốc chỉ có khóa chính. Khóa trong transaction để hai tab không tạo trùng cấu hình của một khách.
        db.ChangeTracker.Clear();
        await using var transaction = db.Database.IsSqlServer() ? await db.Database.BeginTransactionAsync(ct) : null;
        var pages = db.Database.IsSqlServer()
            ? db.MyTrackingPages.FromSqlInterpolated($"SELECT * FROM dbo.TCustomer_MyTracking WITH (UPDLOCK, HOLDLOCK) WHERE CustomerID = {customerId}")
            : db.MyTrackingPages.Where(p => p.CustomerId == customerId);
        var page = await pages.Include(p => p.Banners).FirstOrDefaultAsync(ct);
        if (page is null)
            db.MyTrackingPages.Add(page = new MyTrackingPage(customerId, json));
        else
        {
            // Khóa ngoại cũ cho NULL: xóa rõ các ảnh bị thay thế, tránh để lại dòng mồ côi.
            db.MyTrackingBanners.RemoveRange(page.Banners);
            page.Update(json);
        }
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (DbConcurrency.IsUniqueViolation(ex))
        {
            // Tương thích database vẫn còn ràng buộc unique từ phiên bản portal trước.
            db.ChangeTracker.Clear();
            return IdentityErrors.MyTrackingChanged;
        }
        return ToDto(page, customerId, customer);
    }

    public async Task<Result<PublicMyTrackingDto>> Handle(GetCustomerMyTrackingQuery q, CancellationToken ct)
    {
        if (await customers.GetByCodeAsync(q.CustomerCode, ct) is not { } customer) return IdentityErrors.MyTrackingNotFound;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners)
            .FirstOrDefaultAsync(p => p.CustomerId == customer.Id, ct);
        return page is null
            ? IdentityErrors.MyTrackingNotFound
            : new PublicMyTrackingDto(new RawJson(images.ForDisplay(MyTrackingJson.WithCompanyName(page.ConfigJson, customer.TrackingName))), customer.Code);
    }

    /// <summary>Mã khách dùng trong link /tracking/{mã khách}/...; khách thiếu mã thì dùng CustomerID.</summary>
    private static string CustomerCode(long customerId, CustomerSummary? customer)
    {
        var code = customer?.Code?.Trim();
        return string.IsNullOrEmpty(code) ? customerId.ToString(System.Globalization.CultureInfo.InvariantCulture) : code;
    }

    private MyTrackingDto ToDto(MyTrackingPage p, long customerId, CustomerSummary? customer) =>
        new(new RawJson(images.ForDisplay(MyTrackingJson.WithCompanyName(p.ConfigJson, customer?.TrackingName))),
            CustomerCode(customerId, customer), customer?.TrackingName ?? "");
}
