using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Application;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Infrastructure;
using Xunit;

namespace VietAnExpress.Identity.Tests;
public class SenderProfileWriteTests
{
    [Fact]
    public async Task Xoa_email_da_duyet_khong_hien_lai_email_cu_tu_cot_du_phong()
    {
        await using var db = new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var row = new LegacyCustomer();
        db.Customers.Add(row);
        db.Entry(row).Property(x => x.Id).CurrentValue = 42;
        db.Entry(row).Property(x => x.CompanyName).CurrentValue = "Original company";
        db.Entry(row).Property(x => x.ContactEmail).CurrentValue = "contact@example.test";
        db.Entry(row).Property(x => x.Email).CurrentValue = "legacy@example.test";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var api = new CustomersApi(db);
        Assert.Equal("contact@example.test", (await api.GetByIdAsync(42, TestContext.Current.CancellationToken))!.Email);
        var updated = await api.UpdateSenderInfoAsync(42, new SenderInfoUpdate(Email: ""), TestContext.Current.CancellationToken);
        Assert.Null(updated!.Email);
        Assert.Null(row.ContactEmail);
        Assert.Null(row.Email);
        Assert.Equal("Original company", row.CompanyName);
    }
}
