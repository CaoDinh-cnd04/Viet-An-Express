using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>dbo.YeuCauSuaNguoiGui — bảng mới (người dùng đã đồng ý): yêu cầu sửa thông tin người gửi chờ quản trị Việt An duyệt.</summary>
internal sealed class SenderInfoRequestConfiguration : IEntityTypeConfiguration<SenderInfoRequest>
{
    public void Configure(EntityTypeBuilder<SenderInfoRequest> b)
    {
        b.ToTable("YeuCauSuaNguoiGui", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.CustomerId).HasColumnName("CustomerID");
        b.Property(x => x.CustomerCode).HasColumnName("Ma_Khach").HasMaxLength(150);
        b.Property(x => x.SenderUserName).HasColumnName("Nguoi_Gui").HasMaxLength(100);

        b.Property(x => x.OldCompanyName).HasColumnName("Ten_Cong_Ty_Cu").HasMaxLength(SenderInfo.CompanyNameMaxLength);
        b.Property(x => x.OldContactName).HasColumnName("Nguoi_Lien_He_Cu").HasMaxLength(SenderInfo.ContactNameMaxLength);
        b.Property(x => x.OldPhone).HasColumnName("Dien_Thoai_Cu").HasMaxLength(150);
        b.Property(x => x.OldEmail).HasColumnName("Email_Cu").HasMaxLength(150);
        b.Property(x => x.OldAddress).HasColumnName("Dia_Chi_Cu").HasMaxLength(SenderInfo.AddressMaxLength);
        b.Property(x => x.OldTaxCode).HasColumnName("Ma_So_Thue_Cu").HasMaxLength(150);

        b.Property(x => x.NewCompanyName).HasColumnName("Ten_Cong_Ty_Moi").HasMaxLength(SenderInfo.CompanyNameMaxLength);
        b.Property(x => x.NewContactName).HasColumnName("Nguoi_Lien_He_Moi").HasMaxLength(SenderInfo.ContactNameMaxLength);
        b.Property(x => x.NewPhone).HasColumnName("Dien_Thoai_Moi").HasMaxLength(150);
        b.Property(x => x.NewEmail).HasColumnName("Email_Moi").HasMaxLength(150);
        b.Property(x => x.NewAddress).HasColumnName("Dia_Chi_Moi").HasMaxLength(SenderInfo.AddressMaxLength);
        b.Property(x => x.NewTaxCode).HasColumnName("Ma_So_Thue_Moi").HasMaxLength(150);

        b.Property(x => x.Note).HasColumnName("Ghi_Chu").HasMaxLength(SenderInfoRequest.NoteMaxLength);
        b.Property(x => x.Status).HasColumnName("Trang_Thai").HasMaxLength(SenderInfoRequestStatus.MaxLength).IsUnicode(false).IsConcurrencyToken();
        b.Property(x => x.RejectReason).HasColumnName("Ly_Do_Tu_Choi").HasMaxLength(SenderInfoRequest.ReasonMaxLength);
        b.Property(x => x.ReviewedBy).HasColumnName("Nguoi_Duyet").HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasColumnName("Ngay_Tao").HasColumnType("datetime");
        b.Property(x => x.ReviewedAt).HasColumnName("Ngay_Duyet").HasColumnType("datetime");

        b.Ignore(x => x.Old);
        b.Ignore(x => x.New);
        b.Ignore(x => x.IsPending);
        b.HasIndex(x => new { x.CustomerId, x.CreatedAt }).HasDatabaseName("IX_YeuCauSuaNguoiGui_CustomerID_Ngay_Tao");
        b.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_YeuCauSuaNguoiGui_Trang_Thai_Ngay_Tao");
        b.HasIndex(x => x.CustomerId).IsUnique().HasFilter("[Trang_Thai] = 'pending'").HasDatabaseName("UX_YeuCauSuaNguoiGui_Pending_CustomerID");
    }
}
