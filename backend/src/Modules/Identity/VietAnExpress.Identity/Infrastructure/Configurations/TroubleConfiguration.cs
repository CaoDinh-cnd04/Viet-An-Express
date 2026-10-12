using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>dbo.SuCo — bảng mới (người dùng đã đồng ý): sự cố khách báo cho vận đơn.</summary>
internal sealed class TroubleConfiguration : IEntityTypeConfiguration<Trouble>
{
    public void Configure(EntityTypeBuilder<Trouble> b)
    {
        b.ToTable("SuCo", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.CustomerCode).HasColumnName("Ma_Khach").HasMaxLength(50);
        b.Property(x => x.CompanyName).HasColumnName("Ten_Cong_Ty").HasMaxLength(250);
        b.Property(x => x.StaffId).HasColumnName("StaffID");
        b.Property(x => x.SenderUserName).HasColumnName("Nguoi_Gui").HasMaxLength(100);
        b.Property(x => x.Bill).HasColumnName("Ma_Van_Don").HasMaxLength(Trouble.BillMaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.ConsigneeName).HasColumnName("Nguoi_Nhan").HasMaxLength(Trouble.NameMaxLength);
        b.Property(x => x.Country).HasColumnName("Quoc_Gia").HasMaxLength(Trouble.CountryMaxLength);
        b.Property(x => x.Type).HasColumnName("Loai_Su_Co").HasMaxLength(Trouble.TypeMaxLength).IsRequired();
        b.Property(x => x.Priority).HasColumnName("Muc_Do").HasMaxLength(TroublePriority.MaxLength).IsUnicode(false);
        b.Property(x => x.Description).HasColumnName("Mo_Ta").HasMaxLength(Trouble.DescriptionMaxLength).IsRequired();
        b.Property(x => x.Requester).HasColumnName("Nguoi_Yeu_Cau").HasMaxLength(Trouble.RequesterMaxLength);
        b.Property(x => x.Contact).HasColumnName("Lien_He").HasMaxLength(Trouble.ContactMaxLength);
        b.Property(x => x.Status).HasColumnName("Trang_Thai").HasMaxLength(TroubleStatus.MaxLength).IsUnicode(false);
        b.Property(x => x.CreatedAt).HasColumnName("Ngay_Tao").HasColumnType("datetime");
        b.Property(x => x.UpdatedAt).HasColumnName("Ngay_Cap_Nhat").HasColumnType("datetime");
        b.Ignore(x => x.Code);
        b.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.TroubleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CustomerId, x.UpdatedAt }).HasDatabaseName("IX_SuCo_CustomerID_Ngay_Cap_Nhat");
    }
}

/// <summary>dbo.SuCo_TraoDoi — lịch sử trao đổi khách ↔ Việt An của 1 sự cố.</summary>
internal sealed class TroubleMessageConfiguration : IEntityTypeConfiguration<TroubleMessage>
{
    public void Configure(EntityTypeBuilder<TroubleMessage> b)
    {
        b.ToTable("SuCo_TraoDoi", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.TroubleId).HasColumnName("SuCo_ID");
        b.Property(x => x.FromVietAn).HasColumnName("Tu_Viet_An");
        b.Property(x => x.Author).HasColumnName("Nguoi_Viet").HasMaxLength(150);
        b.Property(x => x.Message).HasColumnName("Noi_Dung").HasMaxLength(TroubleMessage.MessageMaxLength);
        b.Property(x => x.NewStatus).HasColumnName("Trang_Thai_Moi").HasMaxLength(TroubleStatus.MaxLength).IsUnicode(false);
        b.Property(x => x.CreatedAt).HasColumnName("Ngay_Tao").HasColumnType("datetime");
    }
}
