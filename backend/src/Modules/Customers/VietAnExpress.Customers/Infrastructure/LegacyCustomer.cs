using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAnExpress.Customers.Infrastructure;

/// <summary>
/// Hồ sơ khách hàng — bảng <c>dbo.TCustomer</c> của hệ thống cũ (không migration, không đổi cấu trúc).
/// Chỉ map các cột portal cần. Portal chỉ ghi thông tin người gửi khi quản trị Việt An duyệt yêu cầu sửa của khách.
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

    /// <summary>Độ dài cột trong dbo.TCustomer.</summary>
    public const int CompanyNameMaxLength = 250;
    public const int TextMaxLength = 150;
    public const int AddressMaxLength = 250;

    /// <summary>Ghi các trường khác null (chuỗi rỗng → NULL). Trả về true nếu có cột thay đổi.</summary>
    public bool UpdateSenderInfo(VietAnExpress.Customers.Contracts.SenderInfoUpdate u)
    {
        var changed = false;
        Set(u.CompanyName, CompanyName, v => CompanyName = v);
        Set(u.ContactName, ContactName, v => ContactName = v);
        Set(u.Phone, ContactPhone, v => ContactPhone = v);
        Set(u.Email, ContactEmail, v => ContactEmail = v);
        // Clearing the logical email must also clear its legacy fallback; otherwise the old value reappears.
        if (u.Email is not null && string.IsNullOrWhiteSpace(u.Email)) Set("", Email, v => Email = v);
        Set(u.Address, Address1, v => Address1 = v);
        Set(u.TaxCode, TaxCode, v => TaxCode = v);
        return changed;

        void Set(string? value, string? current, Action<string?> write)
        {
            if (value is null) return;
            var next = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (next == current) return;
            write(next);
            changed = true;
        }
    }
}

internal sealed class LegacyCustomerConfiguration : IEntityTypeConfiguration<LegacyCustomer>
{
    public void Configure(EntityTypeBuilder<LegacyCustomer> b)
    {
        b.ToTable("TCustomer", "dbo", t => t.ExcludeFromMigrations());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("CustomerID");
        b.Property(x => x.Code).HasColumnName("CustomerCode");
        b.Property(x => x.CompanyName).HasColumnName("CustomerName").HasMaxLength(LegacyCustomer.CompanyNameMaxLength);
        b.Property(x => x.ContactName).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.ContactPhone).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.ContactEmail).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.Address1).HasMaxLength(LegacyCustomer.AddressMaxLength);
        b.Property(x => x.TaxCode).HasMaxLength(LegacyCustomer.TextMaxLength);
    }
}

/// <summary>dbo.TCustomer — không có migration; chỉ ghi thông tin người gửi đã được duyệt.</summary>
internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<LegacyCustomer> Customers => Set<LegacyCustomer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
}
