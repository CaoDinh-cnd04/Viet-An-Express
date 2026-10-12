using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>dbo.GopY — bảng mới (người dùng đã đồng ý): góp ý khách gửi từ portal.</summary>
internal sealed class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> b)
    {
        b.ToTable("GopY", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.CustomerCode).HasColumnName("Ma_Khach").HasMaxLength(50);
        b.Property(x => x.CompanyName).HasColumnName("Ten_Cong_Ty").HasMaxLength(250);
        b.Property(x => x.StaffId).HasColumnName("StaffID");
        b.Property(x => x.SenderUserName).HasColumnName("Nguoi_Gui").HasMaxLength(100);
        b.Property(x => x.Message).HasColumnName("Noi_Dung").HasMaxLength(Feedback.MessageMaxLength).IsRequired();
        b.Property(x => x.Contact).HasColumnName("Lien_He").HasMaxLength(Feedback.ContactMaxLength);
        b.Property(x => x.Rating).HasColumnName("So_Sao").HasColumnType("tinyint").HasConversion<byte?>();
        b.Property(x => x.SurveyJson).HasColumnName("Khao_Sat"); // nvarchar(max), JSON câu trả lời khảo sát
        b.Property(x => x.IsSeen).HasColumnName("Da_Xem");
        b.Property(x => x.IsHiddenByCustomer).HasColumnName("Khach_Da_An").HasDefaultValue(false);
        b.Property(x => x.CreateDate).HasColumnName("Ngay_Gui").HasColumnType("datetime");
        b.Property(x => x.SeenAt).HasColumnName("Ngay_Xem").HasColumnType("datetime");

        b.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.FeedbackId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CustomerId, x.CreateDate }).HasDatabaseName("IX_GopY_CustomerID_Ngay_Gui");
    }
}

/// <summary>dbo.GopY_HinhAnh — ảnh đính kèm góp ý (tối đa 5 ảnh / góp ý, mỗi ảnh ≤ 5 MB).</summary>
internal sealed class FeedbackImageConfiguration : IEntityTypeConfiguration<FeedbackImage>
{
    public void Configure(EntityTypeBuilder<FeedbackImage> b)
    {
        b.ToTable("GopY_HinhAnh", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.FeedbackId).HasColumnName("GopY_ID");
        b.Property(x => x.FileName).HasColumnName("Ten_File").HasMaxLength(255);
        b.Property(x => x.ContentType).HasColumnName("Loai_File").HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.Size).HasColumnName("Kich_Thuoc");
        b.Property(x => x.Data).HasColumnName("Du_Lieu").IsRequired();
    }
}
