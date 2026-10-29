using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.MyTracking;

/// <param name="Config">Cấu hình dạng JSON object như frontend gửi (backend chỉ kiểm tra là object và ≤ 2 MB).</param>
internal sealed record SaveMyTrackingCommand(JsonElement Config) : IRequest<Result<MyTrackingDto>>;

internal sealed class SaveMyTrackingHandler(IdentityDbContext db, ICustomersApi customers, ICurrentUser user, MyTrackingImageStore images)
    : IRequestHandler<SaveMyTrackingCommand, Result<MyTrackingDto>>
{
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
            return MyTrackingErrors.MyTrackingChanged;
        }
        return MyTrackingMapper.ToDto(page, customerId, customer, images);
    }

    public async Task<Result<MyTrackingDto>> Handle(SaveMyTrackingCommand cmd, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        if (!await db.IsMyTrackingAllowedAsync(customerId, ct)) return MyTrackingErrors.MyTrackingNotAllowed;
        if (cmd.Config.ValueKind != JsonValueKind.Object) return MyTrackingErrors.InvalidTrackingConfig;
        var json = cmd.Config.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MyTrackingPage.ConfigMaxBytes) return MyTrackingErrors.InvalidTrackingConfig;
        // Tên công ty không do khách sửa: luôn lưu theo hồ sơ khách.
        var customer = await customers.GetByIdAsync(customerId, ct);
        json = MyTrackingJson.WithCompanyName(json, customer?.TrackingName);
        try { json = await images.PrepareAsync(json, ct); }
        catch (FormatException) { return MyTrackingErrors.InvalidTrackingConfig; }
        if (MyTrackingChannels.HasTooLongValue(json)) return MyTrackingErrors.InvalidTrackingConfig;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(() => SaveAsync(customerId, customer, json, ct));
    }
}
