using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>dbo.ThongBao — bảng mới (người dùng đã đồng ý): thông báo quản trị Việt An gửi khách.</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("ThongBao", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.Title).HasColumnName("Tieu_De").HasMaxLength(Notification.TitleMaxLength).IsRequired();
        b.Property(x => x.Body).HasColumnName("Noi_Dung").HasMaxLength(Notification.BodyMaxLength).IsRequired();
        b.Property(x => x.Link).HasColumnName("Duong_Dan").HasMaxLength(Notification.LinkMaxLength);
        b.Property(x => x.Important).HasColumnName("Quan_Trong");
        b.Property(x => x.CreatedAt).HasColumnName("Ngay_Tao").HasColumnType("datetime");
        b.Property(x => x.CreatedBy).HasColumnName("Nguoi_Tao").HasMaxLength(100);
        b.Property(x => x.EmailStatus).HasColumnName("Email_Trang_Thai").HasMaxLength(NotificationEmailStatus.MaxLength).IsUnicode(false);
        b.Property(x => x.EmailRecipients).HasColumnName("Email_So_Nguoi_Nhan");
        b.Property(x => x.EmailSent).HasColumnName("Email_Da_Gui");
        b.Property(x => x.EmailError).HasColumnName("Email_Loi").HasMaxLength(Notification.ErrorMaxLength);
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_ThongBao_Ngay_Tao");
    }
}

/// <summary>dbo.ThongBao_DaDoc — tài khoản (chính / con) đã đọc thông báo; xóa thông báo thì xóa theo.</summary>
internal sealed class NotificationReadConfiguration : IEntityTypeConfiguration<NotificationRead>
{
    public void Configure(EntityTypeBuilder<NotificationRead> b)
    {
        b.ToTable("ThongBao_DaDoc", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.NotificationId).HasColumnName("ThongBao_ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.StaffId).HasColumnName("TaiKhoanCon_ID");
        b.Property(x => x.ReadAt).HasColumnName("Ngay_Doc").HasColumnType("datetime");
        b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CustomerId, x.StaffId, x.NotificationId }).IsUnique().HasDatabaseName("UX_ThongBao_DaDoc_TaiKhoan");
    }
}
