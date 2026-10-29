using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Ecommerce.Contracts;

namespace VietAnExpress.Ecommerce.Stores;

/// <summary>dbo.KenhTMDT — bảng mới (người dùng đã đồng ý), tên cột theo kiểu bảng cũ.</summary>
internal sealed class SalesChannelConfiguration : IEntityTypeConfiguration<SalesChannel>
{
    public const string Table = "KenhTMDT";

    public void Configure(EntityTypeBuilder<SalesChannel> b)
    {
        b.ToTable(Table, "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        b.Property(x => x.Code).HasColumnName("Ma_Kenh").HasMaxLength(SalesChannel.CodeMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.Name).HasColumnName("Ten_Kenh").HasMaxLength(SalesChannel.NameMaxLength).IsRequired();
        b.Property(x => x.AuthType).HasColumnName("Kieu_Ket_Noi").HasMaxLength(SalesChannel.AuthTypeMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.Website).HasColumnName("Website").HasMaxLength(SalesChannel.WebsiteMaxLength).IsUnicode(false);
        b.Property(x => x.IsActive).HasColumnName("Dang_Hoat_Dong");
        b.Property(x => x.SupportsTrackingPush).HasColumnName("Day_Tracking");
        b.Property(x => x.SortOrder).HasColumnName("Thu_Tu");
        b.Property(x => x.CreateDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        b.Property(x => x.ModifyDate).HasColumnType("datetime");

        // Ma_Kenh là khóa thay thế (unique) — dbo.KetNoiTMDT tham chiếu theo mã thay vì ID.
        b.HasAlternateKey(x => x.Code).HasName("AK_KenhTMDT_Ma_Kenh");

        // Seed giữ nguyên để khớp dữ liệu sẵn có; portal chưa có adapter kênh nào (StoreConnectionHandlers luôn báo chưa hỗ trợ).
        // Shopee / Lazada: đơn xuyên biên giới do sàn chỉ định vận chuyển → chỉ nhận đơn, không đẩy tracking.
        b.HasData(
            SalesChannel.Seed(1, SalesChannelCodes.Shopify, "Shopify", SalesChannel.OAuth2, "https://www.shopify.com", isActive: true, supportsTrackingPush: true),
            SalesChannel.Seed(2, SalesChannelCodes.TikTok, "TikTok Shop", SalesChannel.OAuth2, "https://seller.tiktokshop.com", isActive: false, supportsTrackingPush: true),
            SalesChannel.Seed(3, SalesChannelCodes.Amazon, "Amazon", SalesChannel.OAuth2, "https://sellercentral.amazon.com", isActive: false, supportsTrackingPush: true),
            SalesChannel.Seed(4, SalesChannelCodes.Ebay, "eBay", SalesChannel.OAuth2, "https://www.ebay.com", isActive: false, supportsTrackingPush: true),
            SalesChannel.Seed(5, SalesChannelCodes.Etsy, "Etsy", SalesChannel.OAuth2, "https://www.etsy.com", isActive: false, supportsTrackingPush: true),
            SalesChannel.Seed(6, SalesChannelCodes.WooCommerce, "WooCommerce", SalesChannel.ApiKey, "https://woocommerce.com", isActive: false, supportsTrackingPush: true),
            SalesChannel.Seed(7, SalesChannelCodes.Shopee, "Shopee", SalesChannel.OAuth2, "https://banhang.shopee.vn", isActive: false, supportsTrackingPush: false),
            SalesChannel.Seed(8, SalesChannelCodes.Lazada, "Lazada", SalesChannel.OAuth2, "https://sellercenter.lazada.vn", isActive: false, supportsTrackingPush: false));
    }
}
