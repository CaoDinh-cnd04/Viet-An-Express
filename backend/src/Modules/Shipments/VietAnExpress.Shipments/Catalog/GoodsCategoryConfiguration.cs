using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>dbo.NhomHangHoa — bảng mới (người dùng đã đồng ý), tên cột theo kiểu bảng cũ.</summary>
internal sealed class GoodsCategoryConfiguration : IEntityTypeConfiguration<GoodsCategory>
{
    public const string Table = "NhomHangHoa";

    public void Configure(EntityTypeBuilder<GoodsCategory> b)
    {
        b.ToTable(Table, "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.Name).HasColumnName("Ten_Nhom").HasMaxLength(GoodsCategory.NameMaxLength).IsRequired();
        b.Property(x => x.IsFavorite).HasColumnName("Yeu_Thich").HasDefaultValue(false);
        b.Property(x => x.SortOrder).HasColumnName("Thu_Tu").HasDefaultValue(0);
        b.Property(x => x.CreateDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        b.Property(x => x.ModifyDate).HasColumnType("datetime");

        b.HasIndex(x => new { x.CustomerId, x.Name }).HasDatabaseName("IX_NhomHangHoa_CustomerID_Ten_Nhom");
    }
}
