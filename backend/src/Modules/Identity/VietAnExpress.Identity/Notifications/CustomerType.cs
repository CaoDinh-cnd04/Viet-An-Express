using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Danh mục loại khách của hệ thống cũ (<c>dbo.TCustomerType</c>, chỉ đọc) — tên loại khi đăng thông báo theo loại khách.</summary>
internal sealed class CustomerType
{
    private CustomerType() { } // EF Core

    public CustomerType(int id, string? name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; private set; }
    public string? Name { get; private set; }
}

internal sealed class CustomerTypeConfiguration : IEntityTypeConfiguration<CustomerType>
{
    public void Configure(EntityTypeBuilder<CustomerType> b)
    {
        b.ToTable("TCustomerType", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("CustomerTypeID").ValueGeneratedNever();
        b.Property(x => x.Name).HasColumnName("CustomerTypeName").HasMaxLength(50);
    }
}
