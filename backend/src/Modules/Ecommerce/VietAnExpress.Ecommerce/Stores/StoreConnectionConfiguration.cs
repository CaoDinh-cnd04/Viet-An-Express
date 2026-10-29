using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAnExpress.Ecommerce.Stores;

/// <summary>dbo.KetNoiTMDT — bảng mới (người dùng đã đồng ý), tên cột theo kiểu bảng cũ.</summary>
internal sealed class StoreConnectionConfiguration : IEntityTypeConfiguration<StoreConnection>
{
    public const string Table = "KetNoiTMDT";

    public void Configure(EntityTypeBuilder<StoreConnection> b)
    {
        var statuses = string.Join(", ", StoreConnection.Statuses.Select(s => $"'{s}'"));
        b.ToTable(Table, "dbo", t => t.HasCheckConstraint("CK_KetNoiTMDT_Trang_Thai", $"[Trang_Thai] IN ({statuses})"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.ChannelCode).HasColumnName("Ma_Kenh").HasMaxLength(SalesChannel.CodeMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.ShopId).HasColumnName("Ma_Shop").HasMaxLength(StoreConnection.ShopIdMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.ShopName).HasColumnName("Ten_Shop").HasMaxLength(StoreConnection.ShopNameMaxLength).IsRequired();
        b.Property(x => x.ShopDomain).HasColumnName("Ten_Mien_Shop").HasMaxLength(StoreConnection.ShopDomainMaxLength).IsUnicode(false);
        b.Property(x => x.ShopCipher).HasColumnName("Shop_Cipher").HasMaxLength(StoreConnection.ShopIdMaxLength).IsUnicode(false);
        b.Property(x => x.Region).HasColumnName("Khu_Vuc").HasMaxLength(StoreConnection.RegionMaxLength).IsUnicode(false);
        b.Property(x => x.Currency).HasColumnName("Tien_Te").HasMaxLength(StoreConnection.CurrencyMaxLength).IsUnicode(false).IsFixedLength();
        b.Property(x => x.Scopes).HasColumnName("Quyen_Truy_Cap").HasMaxLength(StoreConnection.ScopesMaxLength).IsUnicode(false);
        b.Property(x => x.AccessTokenEncrypted).HasColumnName("Access_Token_Ma_Hoa").IsUnicode(false);
        b.Property(x => x.AccessTokenExpiresAt).HasColumnName("Access_Token_Het_Han").HasColumnType("datetime");
        b.Property(x => x.RefreshTokenEncrypted).HasColumnName("Refresh_Token_Ma_Hoa").IsUnicode(false);
        b.Property(x => x.RefreshTokenExpiresAt).HasColumnName("Refresh_Token_Het_Han").HasColumnType("datetime");
        b.Property(x => x.WebhookIds).HasColumnName("Webhook_IDs").HasMaxLength(StoreConnection.WebhookIdsMaxLength).IsUnicode(false);
        b.Property(x => x.Status).HasColumnName("Trang_Thai").HasMaxLength(StoreConnection.StatusMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.ConnectedAt).HasColumnName("Ngay_Ket_Noi").HasColumnType("datetime");
        b.Property(x => x.LastSyncAt).HasColumnName("Dong_Bo_Lan_Cuoi").HasColumnType("datetime");
        b.Property(x => x.LastError).HasColumnName("Loi_Gan_Nhat").HasMaxLength(StoreConnection.LastErrorMaxLength);
        b.Property(x => x.LastErrorAt).HasColumnName("Ngay_Loi").HasColumnType("datetime");
        b.Property(x => x.DisconnectedAt).HasColumnName("Ngay_Ngat_Ket_Noi").HasColumnType("datetime");
        b.Property(x => x.CreateDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        b.Property(x => x.ModifyDate).HasColumnType("datetime");

        b.HasOne<SalesChannel>().WithMany().HasForeignKey(x => x.ChannelCode).HasPrincipalKey(c => c.Code)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_KetNoiTMDT_KenhTMDT_Ma_Kenh");

        // 1 shop chỉ gắn 1 lần với 1 khách; kết nối lại thì cập nhật dòng cũ.
        b.HasIndex(x => new { x.CustomerId, x.ChannelCode, x.ShopId }).IsUnique().HasDatabaseName("UX_KetNoiTMDT_CustomerID_Ma_Kenh_Ma_Shop");
        // Webhook từ sàn chỉ mang mã shop → tìm kết nối theo (kênh, shop).
        b.HasIndex(x => new { x.ChannelCode, x.ShopId }).HasDatabaseName("IX_KetNoiTMDT_Ma_Kenh_Ma_Shop");
    }
}
