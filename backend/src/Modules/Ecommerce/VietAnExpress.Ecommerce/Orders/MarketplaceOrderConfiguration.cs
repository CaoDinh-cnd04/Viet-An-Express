using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Ecommerce.Stores;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>dbo.DonTMDT — bảng mới (người dùng đã đồng ý), tên cột theo kiểu bảng cũ.</summary>
internal sealed class MarketplaceOrderConfiguration : IEntityTypeConfiguration<MarketplaceOrder>
{
    public const string Table = "DonTMDT";

    public void Configure(EntityTypeBuilder<MarketplaceOrder> b)
    {
        var statuses = string.Join(", ", MarketplaceOrder.Statuses.Select(s => $"'{s}'"));
        b.ToTable(Table, "dbo", t => t.HasCheckConstraint("CK_DonTMDT_Trang_Thai", $"[Trang_Thai] IN ({statuses})"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.StoreConnectionId).HasColumnName("KetNoiTMDT_ID");
        b.Property(x => x.Source).HasColumnName("Nguon").HasMaxLength(SalesChannel.CodeMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.PlatformOrderId).HasColumnName("Ma_Don_San").HasMaxLength(MarketplaceOrder.PlatformOrderIdMaxLength).IsUnicode(false);
        b.Property(x => x.OrderName).HasColumnName("So_Don").HasMaxLength(MarketplaceOrder.OrderNameMaxLength).IsRequired();
        b.Property(x => x.Bill).HasColumnName("Bill").HasMaxLength(MarketplaceOrder.BillMaxLength).IsUnicode(false);
        b.Property(x => x.Status).HasColumnName("Trang_Thai").HasMaxLength(MarketplaceOrder.StatusMaxLength).IsUnicode(false).IsRequired();
        b.OwnsOne(x => x.Recipient, r =>
        {
            r.Property(p => p.Name).HasColumnName("Nguoi_Nhan").HasMaxLength(MarketplaceOrder.NameMaxLength);
            r.Property(p => p.Company).HasColumnName("Cong_Ty").HasMaxLength(MarketplaceOrder.NameMaxLength);
            r.Property(p => p.Phone).HasColumnName("Dien_Thoai").HasMaxLength(MarketplaceOrder.PhoneMaxLength).IsUnicode(false);
            r.Property(p => p.Email).HasColumnName("Email").HasMaxLength(MarketplaceOrder.EmailMaxLength).IsUnicode(false);
            r.Property(p => p.Address1).HasColumnName("Dia_Chi1").HasMaxLength(MarketplaceOrder.AddressMaxLength);
            r.Property(p => p.Address2).HasColumnName("Dia_Chi2").HasMaxLength(MarketplaceOrder.AddressMaxLength);
            r.Property(p => p.City).HasColumnName("Thanh_Pho").HasMaxLength(MarketplaceOrder.CityMaxLength);
            r.Property(p => p.Province).HasColumnName("Bang").HasMaxLength(MarketplaceOrder.CityMaxLength);
            r.Property(p => p.PostalCode).HasColumnName("Ma_Buu_Chinh").HasMaxLength(MarketplaceOrder.PostalMaxLength).IsUnicode(false);
            r.Property(p => p.CountryCode).HasColumnName("Ma_Nuoc").HasMaxLength(2).IsUnicode(false).IsFixedLength();
            r.Property(p => p.CountryName).HasColumnName("Ten_Nuoc").HasMaxLength(MarketplaceOrder.CountryNameMaxLength);
        });
        b.Navigation(x => x.Recipient).IsRequired();
        b.Property(x => x.ItemCount).HasColumnName("So_San_Pham");
        b.Property(x => x.WeightKg).HasColumnName("Can_Nang").HasPrecision(10, 3);
        b.Property(x => x.Currency).HasColumnName("Tien_Te").HasMaxLength(3).IsUnicode(false).IsFixedLength();
        b.Property(x => x.TotalAmount).HasColumnName("Tong_Tien").HasPrecision(18, 2);
        b.Property(x => x.ProductsJson).HasColumnName("San_Pham");
        b.Property(x => x.Note).HasColumnName("Ghi_Chu").HasMaxLength(MarketplaceOrder.NoteMaxLength);
        b.Property(x => x.PlacedAt).HasColumnName("Ngay_Dat_San").HasColumnType("datetime");
        b.Property(x => x.Service).HasColumnName("Dich_Vu").HasMaxLength(MarketplaceOrder.ServiceMaxLength);
        b.Property(x => x.Hub).HasColumnName("Hub").HasMaxLength(MarketplaceOrder.HubMaxLength);
        b.Property(x => x.Branch).HasColumnName("Chi_Nhanh").HasMaxLength(MarketplaceOrder.BranchMaxLength);
        b.Property(x => x.CustomsJson).HasColumnName("Hai_Quan");
        b.Property(x => x.EditedAt).HasColumnName("Ngay_Sua").HasColumnType("datetime");
        b.Property(x => x.DeletedAt).HasColumnName("Ngay_Xoa").HasColumnType("datetime");
        b.Property(x => x.ConfirmedAt).HasColumnName("Ngay_Xac_Nhan").HasColumnType("datetime");
        b.Property(x => x.TrackingPushedAt).HasColumnName("Ngay_Day_Tracking").HasColumnType("datetime");
        b.Property(x => x.CreateDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        b.Property(x => x.ModifyDate).HasColumnType("datetime");

        b.HasOne<StoreConnection>().WithMany().HasForeignKey(x => x.StoreConnectionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DonTMDT_KetNoiTMDT_ID");

        // Đồng bộ lại không tạo đơn trùng.
        b.Property(x => x.CreatedByStaffId).HasColumnName("Nhan_Vien_Tao");
        // Lọc đơn của tài khoản con: WHERE CustomerID = @c AND Nhan_Vien_Tao = @staff.
        b.HasIndex(x => new { x.CustomerId, x.CreatedByStaffId }).HasDatabaseName("IX_DonTMDT_CustomerID_Nhan_Vien_Tao");
        b.HasIndex(x => new { x.CustomerId, x.Source, x.PlatformOrderId }).IsUnique()
            .HasFilter("[Ma_Don_San] IS NOT NULL").HasDatabaseName("UX_DonTMDT_CustomerID_Nguon_Ma_Don_San");
        // Tab Đơn hàng: đơn của khách, mới nhất trước.
        b.HasIndex(x => new { x.CustomerId, x.PlacedAt }).HasDatabaseName("IX_DonTMDT_CustomerID_Ngay_Dat_San");
    }
}
