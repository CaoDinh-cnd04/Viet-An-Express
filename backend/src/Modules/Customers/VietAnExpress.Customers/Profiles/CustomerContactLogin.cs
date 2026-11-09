using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Customers.Profiles;

/// <summary>
/// Tên đăng nhập tài khoản con của khách — <c>dbo.TCustomer_NguoiLienLac</c> (ID, CustomerID, TinhCach = tên đăng nhập), chỉ đọc 3 cột
/// để hiện "người tạo đơn" (MaVanDon.Customer_Staff_ID) cho nhân viên Việt An. Module Identity quản lý tài khoản con trên cùng bảng.
/// </summary>
internal sealed class CustomerContactLogin
{
    public long Id { get; init; }
    public long? CustomerId { get; init; }
    public string? UserName { get; init; }
}

internal sealed class CustomerContactLoginConfiguration : IEntityTypeConfiguration<CustomerContactLogin>
{
    public void Configure(EntityTypeBuilder<CustomerContactLogin> b)
    {
        b.ToTable("TCustomer_NguoiLienLac", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.UserName).HasColumnName("TinhCach");
    }
}
