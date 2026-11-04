using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>Giữ cấu trúc bảng gốc: chuỗi tối đa 250 ký tự, ảnh lưu đường dẫn.</summary>
internal sealed class MyTrackingPageConfiguration : IEntityTypeConfiguration<MyTrackingPage>
{
    public void Configure(EntityTypeBuilder<MyTrackingPage> b)
    {
        b.ToTable("TCustomer_MyTracking", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.Title).HasColumnName("UuDai_TieuDe").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.Description).HasColumnName("UuDai_NoiDung").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        // Mạng xã hội: 15 ô CongTruyenThong có sẵn, ứng dụng nào ở ô nào xem MyTrackingChannels.
        // CongTinNhan_* chỉ đọc (dữ liệu cũ) — cổng liên lạc ghi ở TCustomer.CongLienLac_* (MyTrackingBrand).
        for (var i = 1; i <= MyTrackingChannels.SlotCount; i++)
        {
            b.Property<string?>($"CongTinNhan{i:00}").HasColumnName($"CongTinNhan_{i:00}").HasMaxLength(MyTrackingChannels.ValueMaxLength);
            b.Property<string?>($"CongTruyenThong{i:00}").HasColumnName($"CongTruyenThong_{i:00}").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        }
        b.Property(x => x.Background).HasColumnName("Str1").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.CompanyName).HasColumnName("CompanyName").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.Address).HasColumnName("Address").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.Phone).HasColumnName("Tel").HasMaxLength(MyTrackingChannels.ValueMaxLength);

        b.HasMany(x => x.Banners).WithOne().HasForeignKey(x => x.MyTrackingId).OnDelete(DeleteBehavior.ClientCascade);
    }
}

internal sealed class MyTrackingBannerConfiguration : IEntityTypeConfiguration<MyTrackingBanner>
{
    public void Configure(EntityTypeBuilder<MyTrackingBanner> b)
    {
        b.ToTable("TCustomer_MyTracking_Banner", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.MyTrackingId).HasColumnName("MyTrackingID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.ImageKey).HasColumnName("Str1").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.Source).HasColumnName("Url_Hinh").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.Link).HasColumnName("Link").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.Title).HasColumnName("Str2").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        b.Property(x => x.ButtonText).HasColumnName("Str3").HasMaxLength(MyTrackingChannels.ValueMaxLength);
        foreach (var index in b.Metadata.GetIndexes().ToArray()) b.Metadata.RemoveIndex(index);
    }
}
