using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>dbo.MatHangKhachHang — bảng mới (người dùng đã đồng ý), tên cột theo kiểu bảng cũ.</summary>
internal sealed class CatalogMarkConfiguration : IEntityTypeConfiguration<CatalogMark>
{
    public const string Table = "MatHangKhachHang";

    public void Configure(EntityTypeBuilder<CatalogMark> b)
    {
        b.ToTable(Table, "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.Kind).HasColumnName("Loai").HasMaxLength(CatalogMark.KindMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.Key).HasColumnName("Khoa").HasMaxLength(CatalogMark.KeyMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.IsFavorite).HasColumnName("Yeu_Thich").HasDefaultValue(false);
        b.Property(x => x.IsDeleted).HasColumnName("Da_Xoa").HasDefaultValue(false);
        b.Property(x => x.CreateDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        b.Property(x => x.ModifyDate).HasColumnType("datetime");

        b.HasIndex(x => new { x.CustomerId, x.Kind, x.Key }).IsUnique().HasDatabaseName("UX_MatHangKhachHang_CustomerID_Loai_Khoa");
    }
}
