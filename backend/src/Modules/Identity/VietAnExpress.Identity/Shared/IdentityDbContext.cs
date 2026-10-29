using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Shared;

/// <summary>
/// Bảng khách hàng, người liên hệ, liên hệ / form (dbo.TContact — Hỗ trợ &amp; sự cố) và hai bảng TCustomer_MyTracking / TCustomer_MyTracking_Banner
/// của hệ thống cũ; bảng portal: dbo.TThongBao + dbo.TThongBao_DaDoc.
/// Schema "identity" chỉ chứa lịch sử migration.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<CustomerLogin> Logins => Set<CustomerLogin>();
    public DbSet<CustomerType> CustomerTypes => Set<CustomerType>();

    /// <summary>Tài khoản con của nhân viên — người liên hệ có tên đăng nhập trong dbo.TCustomer_NguoiLienLac.</summary>
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();

    /// <summary>Trang MyTracking và ảnh quảng cáo trong hai bảng cũ.</summary>
    public DbSet<MyTrackingPage> MyTrackingPages => Set<MyTrackingPage>();
    public DbSet<MyTrackingBanner> MyTrackingBanners => Set<MyTrackingBanner>();

    /// <summary>Thông báo quản trị Việt An gửi khách (dbo.TThongBao, các tin ở cột JSON) và lượt đã đọc của từng tài khoản (dbo.TThongBao_DaDoc).</summary>
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();

    /// <summary>Hỗ trợ &amp; sự cố — dòng FormType "Portal - …" của bảng cũ dbo.TContact.</summary>
    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
