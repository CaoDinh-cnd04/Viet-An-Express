using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Profiles;
using VietAnExpress.Customers.VietAnUsers;

namespace VietAnExpress.Customers.Shared;

/// <summary>
/// dbo.TCustomer, dbo.USERS, dbo.ACL_ROLES / ACL_ROLES_USERS và tên đăng nhập ở dbo.TCustomer_NguoiLienLac của hệ thống cũ —
/// không có migration, portal chỉ đọc.
/// </summary>
internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<LegacyCustomer> Customers => Set<LegacyCustomer>();
    public DbSet<VietAnUser> VietAnUsers => Set<VietAnUser>();
    public DbSet<VietAnRole> VietAnRoles => Set<VietAnRole>();
    public DbSet<VietAnRoleUser> VietAnRoleUsers => Set<VietAnRoleUser>();
    public DbSet<CustomerContactLogin> ContactLogins => Set<CustomerContactLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
}
