using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Identity.Infrastructure;

/// <summary>
/// Tài khoản đăng nhập của khách — cột Login_UserName / Login_Password trong <c>dbo.TCustomer</c> (dùng chung với hệ thống cũ).
/// Hệ thống cũ so khớp mật khẩu dạng chữ thường, nên portal cũng lưu y như vậy để 2 bên cùng đăng nhập được.
/// </summary>
internal sealed class CustomerLogin
{
    private CustomerLogin() { } // EF Core

    public CustomerLogin(long customerId, string? userName, string? password)
    {
        CustomerId = customerId;
        UserName = userName;
        Password = password;
    }

    public long CustomerId { get; private set; }
    public string? UserName { get; private set; }
    public string? Password { get; private set; }

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public void ChangePassword(string newPassword) => Password = newPassword;
}

internal sealed class CustomerLoginConfiguration : IEntityTypeConfiguration<CustomerLogin>
{
    public void Configure(EntityTypeBuilder<CustomerLogin> b)
    {
        b.ToTable("TCustomer", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.CustomerId);
        b.Property(x => x.CustomerId).HasColumnName("CustomerID").ValueGeneratedNever();
        b.Property(x => x.UserName).HasColumnName("Login_UserName").HasMaxLength(50);
        b.Property(x => x.Password).HasColumnName("Login_Password").HasMaxLength(150);
    }
}

/// <summary>
/// Bảng khách hàng, người liên hệ, liên hệ / form (dbo.TContact — Hỗ trợ &amp; sự cố) và hai bảng TCustomer_MyTracking / TCustomer_MyTracking_Banner
/// của hệ thống cũ; bảng portal: dbo.ThongBao + dbo.ThongBao_DaDoc, dbo.ThongKeSuDung.
/// Schema "identity" chỉ chứa lịch sử migration.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<CustomerLogin> Logins => Set<CustomerLogin>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();

    /// <summary>Tài khoản con của nhân viên — người liên hệ có tên đăng nhập trong dbo.TCustomer_NguoiLienLac.</summary>
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();

    /// <summary>Trang MyTracking và ảnh quảng cáo trong hai bảng cũ.</summary>
    public DbSet<MyTrackingPage> MyTrackingPages => Set<MyTrackingPage>();
    public DbSet<MyTrackingBanner> MyTrackingBanners => Set<MyTrackingBanner>();

    /// <summary>Thông báo quản trị Việt An gửi khách (dbo.ThongBao) và lượt đã đọc của từng tài khoản (dbo.ThongBao_DaDoc).</summary>
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();

    /// <summary>Hỗ trợ &amp; sự cố — dòng FormType "Portal - …" của bảng cũ dbo.TContact.</summary>
    public DbSet<Trouble> Troubles => Set<Trouble>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
