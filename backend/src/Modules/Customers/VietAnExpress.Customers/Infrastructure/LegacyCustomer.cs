using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Customers.Infrastructure;

/// <summary>
/// Hồ sơ khách hàng — bảng <c>dbo.TCustomer</c> của hệ thống cũ (không migration, không đổi cấu trúc).
/// Chỉ map các cột portal cần; portal chỉ đọc (sửa hồ sơ khách do CS làm ở hệ thống cũ / CMS).
/// </summary>
internal sealed class LegacyCustomer
{
    public long Id { get; private set; }
    public string? Code { get; private set; }
    public string? CompanyName { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? Email { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Phone { get; private set; }
    public string? Tel { get; private set; }
    public string? Address1 { get; private set; }
    public string? TaxCode { get; private set; }
    public int? Status { get; private set; }
    /// <summary>Nhân viên Việt An phụ trách khách (Staff_ID → dbo.USERS.ID) — CMS phân công.</summary>
    public int AccountManagerId { get; private set; }

    /// <summary>Độ dài cột trong dbo.TCustomer.</summary>
    public const int CompanyNameMaxLength = 250;
    public const int TextMaxLength = 150;
    public const int AddressMaxLength = 250;
}

internal sealed class LegacyCustomerConfiguration : IEntityTypeConfiguration<LegacyCustomer>
{
    public void Configure(EntityTypeBuilder<LegacyCustomer> b)
    {
        b.ToTable("TCustomer", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("CustomerID");
        b.Property(x => x.Code).HasColumnName("CustomerCode");
        b.Property(x => x.CompanyName).HasColumnName("CustomerName").HasMaxLength(LegacyCustomer.CompanyNameMaxLength);
        b.Property(x => x.ContactName).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.ContactPhone).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.ContactEmail).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.Address1).HasMaxLength(LegacyCustomer.AddressMaxLength);
        b.Property(x => x.TaxCode).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.AccountManagerId).HasColumnName("Staff_ID");
    }
}

/// <summary>dbo.TCustomer và dbo.USERS của hệ thống cũ — không có migration, portal chỉ đọc.</summary>
internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<LegacyCustomer> Customers => Set<LegacyCustomer>();
    public DbSet<VietAnUser> VietAnUsers => Set<VietAnUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
}
