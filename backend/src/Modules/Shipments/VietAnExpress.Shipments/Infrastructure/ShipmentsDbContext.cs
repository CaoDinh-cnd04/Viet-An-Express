using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.Domain;

namespace VietAnExpress.Shipments.Infrastructure;

internal sealed class ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> options) : DbContext(options)
{
    public const string Schema = "shipments";

    /// <summary>Nhóm hàng hóa (dbo.NhomHangHoa) — bảng mới do portal tạo bằng migration.</summary>
    public DbSet<GoodsCategory> GoodsCategories => Set<GoodsCategory>();

    /// <summary>Đánh dấu yêu thích / đã xóa của khách trên thư viện mặt hàng và nhóm chung (dbo.MatHangKhachHang).</summary>
    public DbSet<CatalogMark> CatalogMarks => Set<CatalogMark>();

    /// <summary>Nhân viên (tài khoản con) đã tạo vận đơn (dbo.VanDonNguoiTao) — bảng phụ mới, người dùng đã đồng ý.</summary>

    /// <summary>Bảng vận đơn hệ thống cũ dbo.MaVanDon — không thuộc migration của module.</summary>
    public DbSet<Legacy.LegacyOrder> LegacyOrders => Set<Legacy.LegacyOrder>();

    /// <summary>Chi tiết kiện (dbo.MaVanDon_PCS_DIM) và dòng hàng invoice (dbo.MaVanDon_ChiTietHang) của vận đơn — không thuộc migration.</summary>
    public DbSet<Legacy.LegacyPackageLine> LegacyPackageLines => Set<Legacy.LegacyPackageLine>();
    public DbSet<Legacy.LegacyInvoiceLine> LegacyInvoiceLines => Set<Legacy.LegacyInvoiceLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasSequence<long>(LegacyOrderNumberAllocator.SequenceName).StartsAt(LegacyOrderNumberAllocator.FirstNumber);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentsDbContext).Assembly);
        modelBuilder.ApplyBaseEntityConventions();
    }
}
