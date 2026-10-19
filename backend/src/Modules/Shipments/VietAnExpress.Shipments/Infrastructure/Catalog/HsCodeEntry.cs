using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAnExpress.Shipments.Infrastructure.Catalog;

/// <summary>
/// Danh mục mã HS theo nước nhập khẩu — bảng <c>dbo.Catalog_HsCode</c> do người dùng tạo và nạp dữ liệu (không thuộc migration,
/// portal chỉ đọc). Mỗi dòng: 1 mã quốc gia (US HTSUS 10 số, UK 10 số, AU 8/10 số) gắn với HS6 quốc tế.
/// </summary>
internal sealed class HsCodeEntry
{
    public long Id { get; private set; }
    /// <summary>Mã nước nhập khẩu: US, GB (hoặc UK), AU.</summary>
    public string CountryCode { get; private set; } = "";
    public string Hs6 { get; private set; } = "";
    public string NationalCode { get; private set; } = "";
    public string? NameVi { get; private set; }
    public string NameEn { get; private set; } = "";
    /// <summary>Mô tả dòng mã trong biểu thuế của nước đó.</summary>
    public string? NationalDescription { get; private set; }
    public string? Unit { get; private set; }
    /// <summary>Không trả cho khách: thuế còn phụ thuộc xuất xứ, giá trị lô (xem popup tra HS).</summary>
    public string? DutyRate { get; private set; }
    /// <summary>Ghi chú của Việt An (chất liệu, quy cách đóng gói ảnh hưởng mã…).</summary>
    public string? Note { get; private set; }
    /// <summary>Mặt hàng Việt An đã gửi thực tế và rà soát mã.</summary>
    public bool IsVerified { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

internal sealed class HsCodeEntryConfiguration : IEntityTypeConfiguration<HsCodeEntry>
{
    public void Configure(EntityTypeBuilder<HsCodeEntry> b)
    {
        b.ToTable("Catalog_HsCode", "dbo", t => t.ExcludeFromMigrations());
        b.HasKey(x => x.Id);
        b.Property(x => x.CountryCode).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.Hs6).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.NationalCode).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameVi).HasMaxLength(500);
        b.Property(x => x.NameEn).HasMaxLength(500);
        b.Property(x => x.Unit).HasMaxLength(50);
        b.Property(x => x.DutyRate).HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
    }
}
