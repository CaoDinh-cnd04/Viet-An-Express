using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Orders;
using VietAnExpress.Ecommerce.Stores;

namespace VietAnExpress.Ecommerce.Shared;

/// <summary>Bảng của module nằm ở dbo (tên cột kiểu bảng cũ); schema "ecommerce" chỉ chứa lịch sử migration.</summary>
internal sealed class EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : DbContext(options)
{
    public const string Schema = "ecommerce";

    /// <summary>Danh mục kênh bán (dbo.KenhTMDT) — bảng mới, người dùng đã đồng ý.</summary>
    public DbSet<SalesChannel> SalesChannels => Set<SalesChannel>();

    /// <summary>Shop của khách đã kết nối (dbo.KetNoiTMDT) — bảng mới, người dùng đã đồng ý.</summary>
    public DbSet<StoreConnection> StoreConnections => Set<StoreConnection>();

    /// <summary>Đơn E-commerce (dbo.DonTMDT) — bảng mới, người dùng đã đồng ý.</summary>
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcommerceDbContext).Assembly);
    }
}
