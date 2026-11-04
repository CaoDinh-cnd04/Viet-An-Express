using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>
/// Lưu như trang mytracking.top cũ: ưu đãi / ảnh nền / mạng xã hội / ảnh quảng cáo vào TCustomer_MyTracking (+ _Banner);
/// website, màu, cổng liên lạc, logo vào hồ sơ khách TCustomer (<see cref="MyTrackingBrand"/>). Tên công ty, địa chỉ, điện thoại khách sửa
/// thì lưu ở TCustomer_MyTracking (<see cref="MyTrackingPage.SetContactInfo"/>) — hồ sơ khách TCustomer không đổi.
/// </summary>
internal sealed class SaveMyTrackingHandler(IdentityDbContext db, ICustomersApi customers, ICurrentUser user, MyTrackingImageStore images, TimeProvider clock)
    : IRequestHandler<SaveMyTrackingCommand, Result<MyTrackingDto>>
{
    private const string LogoFolderName = "Logo_ShippingMark";

    /// <summary>
    /// Logo khác logo đang dùng: ảnh vừa tải lên (tệp trong Upload/Logo_ShippingMark) → thêm 1 dòng TCommonFile như hệ thống cũ;
    /// rỗng → bỏ logo; đường dẫn lạ → giữ logo cũ.
    /// </summary>
    private async Task SaveLogoAsync(MyTrackingBrand brand, string? currentLogo, string? logo, CancellationToken ct)
    {
        logo = logo?.Trim() ?? "";
        if (string.Equals(logo, currentLogo ?? "", StringComparison.OrdinalIgnoreCase)) return;
        if (logo.Length == 0) { brand.SetLogo(null); return; }
        if (!logo.StartsWith(MyTrackingBrand.LogoFolder, StringComparison.OrdinalIgnoreCase)) return;
        var name = logo[MyTrackingBrand.LogoFolder.Length..];
        if (images.Find(LogoFolderName, name) is not { } stored) return;
        var file = new LegacyCommonFile(name, (int)new FileInfo(stored.Path).Length, stored.ContentType, VietnamTime.Now(clock));
        db.CommonFiles.Add(file);
        await db.SaveChangesAsync(ct);
        brand.SetLogo(file.Id);
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
        var brandView = await db.MyTrackingBrandAsync(customerId, ct, tracked: true);
        var (brand, currentLogo) = brandView;
        var brandJson = MyTrackingJson.Object(json)["brand"] as JsonObject;
        if (brand is not null)
        {
            brand.Update(brandJson);
            await SaveLogoAsync(brand, currentLogo, MyTrackingJson.Text(brandJson, "logo"), ct);
        }
        if (page is null)
            db.MyTrackingPages.Add(page = new MyTrackingPage(customerId, json));
        else
        {
            // Khóa ngoại cũ cho NULL: xóa rõ các ảnh bị thay thế, tránh để lại dòng mồ côi.
            db.MyTrackingBanners.RemoveRange(page.Banners);
            page.Update(json);
        }
        page.SetContactInfo(brandJson, MyTrackingProfile.From(brandView, customer?.TrackingName));
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
        db.ChangeTracker.Clear();
        return MyTrackingMapper.ToDto(page, await db.MyTrackingBrandAsync(customerId, ct), customerId, customer, images);
    }

    public async Task<Result<MyTrackingDto>> Handle(SaveMyTrackingCommand cmd, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        if (!await db.IsMyTrackingAllowedAsync(customerId, ct)) return MyTrackingErrors.MyTrackingNotAllowed;
        if (cmd.Config.ValueKind != JsonValueKind.Object) return MyTrackingErrors.InvalidTrackingConfig;
        var json = cmd.Config.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MyTrackingPage.ConfigMaxBytes) return MyTrackingErrors.InvalidTrackingConfig;
        var customer = await customers.GetByIdAsync(customerId, ct);
        try { json = await images.PrepareAsync(json, ct); }
        catch (FormatException) { return MyTrackingErrors.InvalidTrackingConfig; }
        if (MyTrackingChannels.HasTooLongValue(json) || !MyTrackingBrand.FitsColumns(MyTrackingJson.Object(json)["brand"] as JsonObject))
            return MyTrackingErrors.InvalidTrackingConfig;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(() => SaveAsync(customerId, customer, json, ct));
    }
}
