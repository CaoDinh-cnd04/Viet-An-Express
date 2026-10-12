using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

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
        b.ToTable("TCustomer", "dbo", t => t.ExcludeFromMigrations());
        b.HasKey(x => x.CustomerId);
        b.Property(x => x.CustomerId).HasColumnName("CustomerID").ValueGeneratedNever();
        b.Property(x => x.UserName).HasColumnName("Login_UserName").HasMaxLength(50);
        b.Property(x => x.Password).HasColumnName("Login_Password").HasMaxLength(150);
    }
}

/// <summary>
/// dbo.TCustomer (bảng cũ, không migration) + bảng mới người dùng đã đồng ý: dbo.TaiKhoanNhanVien (tạm), dbo.MyTrackingCauHinh, dbo.GopY + dbo.GopY_HinhAnh,
/// dbo.ThongBao + dbo.ThongBao_DaDoc, dbo.SuCo + dbo.SuCo_TraoDoi.
/// Schema "identity" chỉ chứa lịch sử migration.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<CustomerLogin> Logins => Set<CustomerLogin>();

    /// <summary>Tài khoản con của nhân viên (dbo.TaiKhoanNhanVien).</summary>
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();

    /// <summary>Trang MyTracking của khách (dbo.MyTrackingCauHinh).</summary>
    public DbSet<MyTrackingPage> MyTrackingPages => Set<MyTrackingPage>();

    /// <summary>Góp ý khách gửi (dbo.GopY) và ảnh đính kèm (dbo.GopY_HinhAnh).</summary>
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FeedbackImage> FeedbackImages => Set<FeedbackImage>();

    /// <summary>Thông báo quản trị Việt An gửi khách (dbo.ThongBao) và lượt đã đọc của từng tài khoản (dbo.ThongBao_DaDoc).</summary>
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();

    /// <summary>Sự cố khách báo (dbo.SuCo) và lịch sử trao đổi (dbo.SuCo_TraoDoi).</summary>
    public DbSet<Trouble> Troubles => Set<Trouble>();
    public DbSet<TroubleMessage> TroubleMessages => Set<TroubleMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
