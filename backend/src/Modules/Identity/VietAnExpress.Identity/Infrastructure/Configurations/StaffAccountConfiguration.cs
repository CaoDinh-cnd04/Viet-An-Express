using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>
/// dbo.TCustomer_NguoiLienLac — bảng người liên hệ của khách ở hệ thống cũ (không thuộc migration, trừ cột Quyen portal thêm).
/// Chỉ dòng có tên đăng nhập (TinhCach) là tài khoản con; người liên hệ thường bị lọc bỏ.
/// </summary>
internal sealed class StaffAccountConfiguration : IEntityTypeConfiguration<StaffAccount>
{
    public void Configure(EntityTypeBuilder<StaffAccount> b)
    {
        b.ToTable("TCustomer_NguoiLienLac", "dbo", t => t.ExcludeFromMigrations());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        // Cột cũ cho phép NULL — map qua field nullable của entity (xem StaffAccount).
        b.Property(x => x.UserName).HasColumnName("TinhCach").HasMaxLength(150).IsRequired(false);
        b.Property(x => x.Password).HasColumnName("Mo_Ta").HasMaxLength(StaffAccount.PasswordMaxLength).IsRequired(false);
        b.Property(x => x.FullName).HasColumnName("Ten").HasMaxLength(StaffAccount.FullNameMaxLength).IsRequired(false);
        b.Property(x => x.Email).HasColumnName("Email").HasMaxLength(StaffAccount.EmailMaxLength);
        b.Property(x => x.Phone).HasColumnName("Di_Dong").HasMaxLength(150);
        b.Property(x => x.Phone2).HasColumnName("Di_Dong2").HasMaxLength(150);
        b.Property(x => x.Email2).HasColumnName("Email2").HasMaxLength(StaffAccount.EmailMaxLength);
        b.Property(x => x.Permissions).HasColumnName("Quyen").HasMaxLength(StaffAccount.PermissionsMaxLength).IsRequired(false);
        // Trang_Thai của hệ thống cũ: 1 = đang hoạt động.
        b.Property(x => x.IsActive).HasColumnName("Trang_Thai").HasConversion(v => v ? 1 : 0, v => v == 1);
        b.Property(x => x.CreateDate).HasColumnName("Create_Date").HasColumnType("datetime");
        // Cột bắt buộc của bảng cũ portal không dùng — ghi 0 khi tạo tài khoản.
        b.Property<int>("Chuc_Vu");
        b.Property<int>("Phong_Ban");
        b.Ignore(x => x.PermissionList);

        b.HasQueryFilter(x => x.UserName != null && x.UserName != "");
    }
}
