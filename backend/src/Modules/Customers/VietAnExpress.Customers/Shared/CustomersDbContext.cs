using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Profiles;
using VietAnExpress.Customers.VietAnUsers;

namespace VietAnExpress.Customers.Shared;

/// <summary>dbo.TCustomer và dbo.USERS của hệ thống cũ — không có migration, portal chỉ đọc.</summary>
internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<LegacyCustomer> Customers => Set<LegacyCustomer>();
    public DbSet<VietAnUser> VietAnUsers => Set<VietAnUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
}
