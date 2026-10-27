using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>
/// dbo.TThongBao — bảng portal (người dùng đã đồng ý; 27/10/2026 gộp ThongBao + ThongBao_Tin + ThongBao_NguoiNhan thành 1 bảng):
/// thông báo quản trị Việt An gửi khách. Các tin nằm ở cột JSON <c>Tin</c>, khách chỉ định ở cột <c>Khach_Nhan</c> dạng ",id,".
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("TThongBao", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.Title).HasColumnName("Tieu_De").HasMaxLength(Notification.TitleMaxLength).IsRequired();
        b.Property(x => x.Body).HasColumnName("Noi_Dung").HasMaxLength(Notification.BodyMaxLength).IsRequired();
        b.Property(x => x.Link).HasColumnName("Duong_Dan").HasMaxLength(Notification.LinkMaxLength);
        b.Property(x => x.Important).HasColumnName("Quan_Trong");
        b.Property(x => x.Pinned).HasColumnName("Ghim");
        b.Property(x => x.CreatedAt).HasColumnName("Ngay_Tao").HasColumnType("datetime");
        b.Property(x => x.CreatedBy).HasColumnName("Nguoi_Tao").HasMaxLength(100);
        b.Property(x => x.PublishAt).HasColumnName("Ngay_Dang").HasColumnType("datetime");
        b.Property(x => x.ExpiresAt).HasColumnName("Ngay_Het_Han").HasColumnType("datetime");
        // Không khai HasDefaultValue: Trang_Thai = 0 (nháp) trùng giá trị mặc định của int, EF sẽ bỏ qua và ghi mặc định của cột.
        b.Property(x => x.Status).HasColumnName("Trang_Thai");
        b.Property(x => x.Audience).HasColumnName("Doi_Tuong").HasMaxLength(NotificationAudience.MaxLength).IsUnicode(false).IsRequired();
        b.Property(x => x.CustomerTypes).HasColumnName("Loai_Khach").HasMaxLength(Notification.CustomerTypesMaxLength).IsUnicode(false);
        b.Property(x => x.Recipients).HasColumnName("Khach_Nhan").IsUnicode(false);
        b.Property(x => x.EmailStatus).HasColumnName("Email_Trang_Thai").HasMaxLength(NotificationEmailStatus.MaxLength).IsUnicode(false);
        b.Property(x => x.EmailRecipients).HasColumnName("Email_So_Nguoi_Nhan");
        b.Property(x => x.EmailSent).HasColumnName("Email_Da_Gui");
        b.Property(x => x.EmailError).HasColumnName("Email_Loi").HasMaxLength(Notification.ErrorMaxLength);
        b.Ignore(x => x.CustomerTypeIds);
        b.Ignore(x => x.RecipientIds);
        // Các tin: mảng JSON [{ Thu_Tu, Tieu_De, Noi_Dung, Chi_Tiet, Hinh_Anh, Duong_Dan }] — Noi_Dung / Chi_Tiet là chữ hoặc HTML đã lọc.
        b.OwnsMany(x => x.Items, i =>
        {
            i.ToJson("Tin");
            i.Property(x => x.SortOrder).HasJsonPropertyName("Thu_Tu");
            i.Property(x => x.Title).HasJsonPropertyName("Tieu_De");
            i.Property(x => x.Body).HasJsonPropertyName("Noi_Dung").IsRequired();
            i.Property(x => x.Detail).HasJsonPropertyName("Chi_Tiet");
            i.Property(x => x.Images).HasJsonPropertyName("Hinh_Anh");
            i.Property(x => x.Link).HasJsonPropertyName("Duong_Dan");
            i.Ignore(x => x.ImagePaths);
        });
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_TThongBao_Ngay_Tao");
        b.HasIndex(x => new { x.Status, x.PublishAt }).HasDatabaseName("IX_TThongBao_Trang_Thai_Ngay_Dang");
    }
}

/// <summary>dbo.TThongBao_DaDoc — tài khoản (chính / con) đã đọc thông báo; xóa thông báo thì xóa theo.</summary>
internal sealed class NotificationReadConfiguration : IEntityTypeConfiguration<NotificationRead>
{
    public void Configure(EntityTypeBuilder<NotificationRead> b)
    {
        b.ToTable("TThongBao_DaDoc", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.NotificationId).HasColumnName("ThongBao_ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.StaffId).HasColumnName("TaiKhoanCon_ID");
        b.Property(x => x.ReadAt).HasColumnName("Ngay_Doc").HasColumnType("datetime");
        b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CustomerId, x.StaffId, x.NotificationId }).IsUnique().HasDatabaseName("UX_TThongBao_DaDoc_TaiKhoan");
    }
}
