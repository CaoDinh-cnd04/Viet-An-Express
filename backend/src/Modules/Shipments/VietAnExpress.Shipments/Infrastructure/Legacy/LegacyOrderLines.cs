using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Shipments.Infrastructure.Legacy;

/// <summary>
/// 1 dòng kiện của vận đơn — bảng <c>dbo.MaVanDon_PCS_DIM</c> (dùng chung với hệ thống cũ, không nằm trong migration).
/// <see cref="WeightKg"/>, <see cref="VolumetricKg"/>, <see cref="ChargeableKg"/> là tổng của cả dòng (giống cột GW_n trong file Excel mẫu).
/// </summary>
internal sealed class LegacyPackageLine
{
    public long Id { get; set; }
    /// <summary>dbo.MaVanDon.ID.</summary>
    public int OrderId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Loại bao bì: CARTON, BAG, PALLET…</summary>
    public string PackType { get; set; } = "";
    public int LengthCm { get; set; }
    public int WidthCm { get; set; }
    public int HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? VolumetricKg { get; set; }
    public decimal? ChargeableKg { get; set; }
}

/// <summary>1 dòng hàng trên invoice của vận đơn — bảng <c>dbo.MaVanDon_ChiTietHang</c> (không nằm trong migration).</summary>
internal sealed class LegacyInvoiceLine
{
    public long Id { get; set; }
    /// <summary>dbo.MaVanDon.ID.</summary>
    public int? OrderId { get; set; }
    public string? DescriptionEn { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
    public string? HsCode { get; set; }
    public string? DescriptionVi { get; set; }
    public string? Manufacturer { get; set; }
    public string? Origin { get; set; }
    public string? Currency { get; set; }
    public string? GrossWeight { get; set; }
    public string? NetWeight { get; set; }
    /// <summary>
    /// Số thứ tự kiện (1..tổng số kiện) của dòng hàng khi tuyến yêu cầu invoice riêng từng kiện (vd Chuyên tuyến Úc / EU / Canada);
    /// null = invoice chung của cả đơn (đơn cũ, hệ thống cũ). Cột portal thêm vào bảng cũ: dbo.MaVanDon_ChiTietHang.Kien.
    /// </summary>
    public int? Package { get; set; }
}

internal sealed class LegacyPackageLineConfiguration : IEntityTypeConfiguration<LegacyPackageLine>
{
    public void Configure(EntityTypeBuilder<LegacyPackageLine> b)
    {
        b.ToTable("MaVanDon_PCS_DIM", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.OrderId).HasColumnName("MaVanDonID");
        b.Property(x => x.Quantity).HasColumnName("SoLuong");
        b.Property(x => x.PackType).HasColumnName("Loai").HasMaxLength(50).IsRequired();
        b.Property(x => x.LengthCm).HasColumnName("Dai");
        b.Property(x => x.WidthCm).HasColumnName("Rong");
        b.Property(x => x.HeightCm).HasColumnName("Cao");
        b.Property(x => x.WeightKg).HasColumnName("TrongLuong").HasPrecision(9, 2);
        b.Property(x => x.VolumetricKg).HasColumnName("QuiDoi").HasPrecision(9, 2);
        b.Property(x => x.ChargeableKg).HasColumnName("ChargeWeight").HasPrecision(9, 2);
    }
}

internal sealed class LegacyInvoiceLineConfiguration : IEntityTypeConfiguration<LegacyInvoiceLine>
{
    public void Configure(EntityTypeBuilder<LegacyInvoiceLine> b)
    {
        b.ToTable("MaVanDon_ChiTietHang", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.OrderId).HasColumnName("MaVanDonID");
        b.Property(x => x.DescriptionEn).HasColumnName("Ten_Hang").HasMaxLength(500);
        b.Property(x => x.Quantity).HasColumnName("So_Luong").HasPrecision(18, 2);
        b.Property(x => x.Unit).HasColumnName("Don_Vi_Tinh").HasMaxLength(50);
        b.Property(x => x.UnitPrice).HasColumnName("Don_Gia").HasPrecision(18, 2);
        b.Property(x => x.Amount).HasColumnName("Thanh_Tien").HasPrecision(18, 2);
        b.Property(x => x.HsCode).HasColumnName("HS_Code").HasMaxLength(500);
        b.Property(x => x.DescriptionVi).HasColumnName("Ten_Hang_VN").HasMaxLength(500);
        b.Property(x => x.Manufacturer).HasColumnName("NSX").HasMaxLength(999);
        b.Property(x => x.Origin).HasColumnName("Nuoc_SX").HasMaxLength(300);
        b.Property(x => x.Currency).HasColumnName("Loai_Tien").HasMaxLength(150);
        b.Property(x => x.GrossWeight).HasMaxLength(150);
        b.Property(x => x.NetWeight).HasMaxLength(150);
        b.Property(x => x.Package).HasColumnName("Kien");
    }
}
