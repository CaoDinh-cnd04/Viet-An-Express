using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>
/// dbo.TContact — bảng liên hệ / form của hệ thống cũ (không thuộc migration). Portal ghi yêu cầu Hỗ trợ &amp; sự cố với FormType "Portal - …"
/// để phần mềm cũ hiện và xử lý; dòng khác của bảng (form khác của hệ thống cũ) bị lọc bỏ.
/// Cột phụ: Des01 = mã vận đơn, Des02 = mức độ, Des03 = người nhận, Des04 = nước, Des05 = ID tài khoản con, KeyWord = loại sự cố / chủ đề.
/// </summary>
internal sealed class TroubleConfiguration : IEntityTypeConfiguration<Trouble>
{
    public void Configure(EntityTypeBuilder<Trouble> b)
    {
        b.ToTable("TContact", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").UseIdentityColumn();
        b.Property(x => x.Kind).HasColumnName("FormType").HasMaxLength(250)
            .HasConversion(k => TroubleKind.FormTypeOf(k), f => TroubleKind.FromFormType(f));
        b.Property(x => x.CustomerId).HasColumnName("CustID");
        b.Property(x => x.CompanyName).HasColumnName("CompanyName").HasMaxLength(350);
        b.Property(x => x.StaffId).HasColumnName("Des05").HasMaxLength(300)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : null,
                v => string.IsNullOrEmpty(v) ? null : long.Parse(v, CultureInfo.InvariantCulture));
        b.Property(x => x.Bill).HasColumnName("Des01").HasMaxLength(300);
        b.Property(x => x.Priority).HasColumnName("Des02").HasMaxLength(300);
        b.Property(x => x.ConsigneeName).HasColumnName("Des03").HasMaxLength(300);
        b.Property(x => x.Country).HasColumnName("Des04").HasMaxLength(300);
        b.Property(x => x.Type).HasColumnName("KeyWord").HasMaxLength(250);
        b.Property(x => x.Title).HasColumnName("Title").HasMaxLength(300);
        b.Property(x => x.Description).HasColumnName("Content").HasColumnType("ntext");
        b.Property(x => x.Requester).HasColumnName("Name").HasMaxLength(250);
        b.Property(x => x.Contact).HasColumnName("Phone").HasMaxLength(250);
        b.Property(x => x.Email).HasColumnName("Email").HasMaxLength(250);
        b.Property(x => x.Status).HasColumnName("Status")
            .HasConversion(s => (int?)TroubleStatus.ToNumber(s), n => TroubleStatus.FromNumber(n));
        b.Property(x => x.CreatedAt).HasColumnName("RegDate").HasColumnType("datetime");
        b.Property(x => x.UpdatedAt).HasColumnName("ModifyDate").HasColumnType("datetime");
        // Cột bắt buộc của bảng cũ (tệp lưu ở hệ thống cũ) — portal không lưu tệp, ghi 0.
        b.Property<long>("FileID");
        b.Property<long>("FileID1");
        b.Property<long>("FileID2");
        b.Property<long>("FileID3");
        b.Ignore(x => x.Code);
        b.Ignore(x => x.Subject);

        b.HasQueryFilter(x => x.Kind == TroubleKind.Incident || x.Kind == TroubleKind.Support || x.Kind == TroubleKind.Feedback);
    }
}
