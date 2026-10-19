using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>Hai bảng MyTracking có sẵn của hệ thống cũ; các cột portal bổ sung do migration chuyển dữ liệu quản lý.</summary>
internal sealed class MyTrackingPageConfiguration : IEntityTypeConfiguration<MyTrackingPage>
{
    public void Configure(EntityTypeBuilder<MyTrackingPage> b)
    {
        b.ToTable("TCustomer_MyTracking", "dbo", t => t.ExcludeFromMigrations());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.Slug).HasColumnName("Duong_Dan").HasMaxLength(MyTrackingPage.SlugMaxLength).IsUnicode(false);
        b.Property(x => x.Title).HasColumnName("UuDai_TieuDe");
        b.Property(x => x.Description).HasColumnName("UuDai_NoiDung");
        b.Property(x => x.CompanyName).HasColumnName("CompanyName");
        b.Property(x => x.Address).HasColumnName("Address");
        b.Property(x => x.Phone).HasColumnName("Tel");
        b.Property(x => x.Logo).HasColumnName("Logo");
        b.Property(x => x.Facebook).HasColumnName("CongTruyenThong_01");
        b.Property(x => x.Instagram).HasColumnName("CongTruyenThong_02");
        b.Property(x => x.X).HasColumnName("CongTruyenThong_03");
        b.Property(x => x.Zalo).HasColumnName("CongTinNhan_01");
        b.Property(x => x.WhatsApp).HasColumnName("CongTinNhan_02");
        b.Property(x => x.Background).HasColumnName("Hinh_Nen");
        b.Property(x => x.ExtraConfigJson).HasColumnName("Cau_Hinh_Bo_Sung");
        b.Property(x => x.IsPublished).HasColumnName("Da_Xuat_Ban");
        b.Property(x => x.CreateDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        b.Property(x => x.ModifyDate).HasColumnType("datetime");
        b.Ignore(x => x.UpdatedAt);
        b.Ignore(x => x.ConfigJson);

        b.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("UX_TCustomer_MyTracking_CustomerID");
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Duong_Dan] IS NOT NULL").HasDatabaseName("UX_TCustomer_MyTracking_Duong_Dan");
        b.HasMany(x => x.Banners).WithOne().HasForeignKey(x => x.MyTrackingId).OnDelete(DeleteBehavior.ClientCascade);
    }
}

internal sealed class MyTrackingBannerConfiguration : IEntityTypeConfiguration<MyTrackingBanner>
{
    public void Configure(EntityTypeBuilder<MyTrackingBanner> b)
    {
        b.ToTable("TCustomer_MyTracking_Banner", "dbo", t => t.ExcludeFromMigrations());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.MyTrackingId).HasColumnName("MyTrackingID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.ImageKey).HasColumnName("Str1");
        b.Property(x => x.Source).HasColumnName("Url_Hinh");
        b.Property(x => x.Link).HasColumnName("Link");
        b.Property(x => x.Title).HasColumnName("Str2");
        b.Property(x => x.ButtonText).HasColumnName("Str3");
        b.Property(x => x.ExtraConfigJson).HasColumnName("Cau_Hinh_Bo_Sung");
        b.HasIndex(x => new { x.MyTrackingId, x.SortOrder }).HasDatabaseName("IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder");
    }
}
