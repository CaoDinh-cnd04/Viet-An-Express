using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Geo;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

// In / xoÃ¡ cÃ¹ng 1 Ä‘Æ¡n nhÃ¡p tá»« 2 request cÃ¹ng lÃºc â€” cháº¡y trÃªn database táº¡m, khÃ´ng bao giá» trÃªn database á»©ng dá»¥ng.
public class DraftConcurrencySqlServerTests(DraftSqlServerFixture fixture) : IClassFixture<DraftSqlServerFixture>
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OrderAccess Access()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.CustomerId).Returns(DraftSqlServerFixture.CustomerId);
        return new OrderAccess(user.Object, Mock.Of<ICustomersApi>());
    }

    private static PrintDraftHandler Print(ShipmentsDbContext db)
    {
        var geo = new Mock<IGeoLookup>();
        geo.Setup(g => g.GetCountriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var remote = new Mock<IRemoteAreaLookup>();
        remote.Setup(r => r.FindAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return new PrintDraftHandler(db, Access(), new LegacyOrderNumberAllocator(db), remote.Object, geo.Object, TimeProvider.System);
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Hai_nguoi_cung_in_1_nhap_chi_1_nguoi_duoc_cap_bill_nguoi_sau_bao_don_da_in()
    {
        var id = await fixture.AddReadyDraftAsync();
        await using var db1 = fixture.CreateDb();
        await using var db2 = fixture.CreateDb();

        var results = await Task.WhenAll(
            Print(db1).Handle(new PrintDraftCommand(id), Ct),
            Print(db2).Handle(new PrintDraftCommand(id), Ct));

        var ok = Assert.Single(results, r => r.IsSuccess);
        var failed = Assert.Single(results, r => r.IsFailure);
        Assert.Equal("DRAFT_CHANGED", failed.Error.Code);

        await using var check = fixture.CreateDb();
        var order = await check.LegacyOrders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == id, Ct);
        Assert.Equal(LegacyOrder.IssuedStatus, order.Status);
        // Bill ngÆ°á»i Ä‘Æ°á»£c in nháº­n chÃ­nh lÃ  bill lÆ°u trong Ä‘Æ¡n â€” khÃ´ng bá»‹ láº§n in thá»© 2 cáº¥p sá»‘ khÃ¡c Ä‘Ã¨ lÃªn.
        Assert.Equal(ok.Value.BillCode, order.VaBill ?? order.OrderNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.NotNull(order.OrderNumber);
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Xoa_nhap_vua_duoc_in_bao_loi_khong_xoa_mat_van_don()
    {
        var id = await fixture.AddReadyDraftAsync();
        await using (var db = fixture.CreateDb())
            Assert.True((await Print(db).Handle(new PrintDraftCommand(id), Ct)).IsSuccess);

        // NgÆ°á»i thá»© 2 váº«n cÃ²n má»Ÿ danh sÃ¡ch nhÃ¡p cÅ© vÃ  báº¥m xoÃ¡.
        await using (var db = fixture.CreateDb())
        {
            var deleted = await new DraftHandlers(db, Access(), Mock.Of<IGeoLookup>(), TimeProvider.System).Handle(new DeleteDraftCommand(id), Ct);
            Assert.True(deleted.IsFailure);
        }

        await using var check = fixture.CreateDb();
        Assert.True(await check.LegacyOrders.AnyAsync(o => o.Id == id, Ct));
    }
}

public class AppLockSqlServerTests(DraftSqlServerFixture fixture) : IClassFixture<DraftSqlServerFixture>
{
    public static bool SqlServerAvailable => DraftConcurrencySqlServerTests.SqlServerAvailable;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Kiem_tra_roi_ghi_trong_khoa_ung_dung_2_request_cung_luc_chi_ghi_1_lan()
    {
        // MÃ´ phá»ng táº¡o tÃ i khoáº£n con cÃ¹ng tÃªn: "chÆ°a cÃ³ thÃ¬ thÃªm" cháº¡y song song 5 láº§n â†’ chá»‰ Ä‘Ãºng 1 dÃ²ng.
        var key = "LOCK-" + Guid.NewGuid().ToString("N")[..8];
        async Task<bool> TryAdd()
        {
            await using var db = fixture.CreateDb();
            return await db.WithAppLockAsync("test:" + key, async () =>
            {
                if (await db.LegacyOrders.IgnoreQueryFilters().AnyAsync(o => o.CustomerBill == key, Ct)) return false;
                await Task.Delay(50, Ct); // ná»›i khoáº£ng giá»¯a kiá»ƒm tra vÃ  ghi Ä‘á»ƒ lá»™ lá»—i náº¿u khÃ´ng khÃ³a
                db.LegacyOrders.Add(new LegacyOrder { CustomerId = DraftSqlServerFixture.CustomerId, CustomerBill = key, Status = LegacyOrder.DraftStatus });
                await db.SaveChangesAsync(Ct);
                return true;
            }, Ct);
        }

        var added = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TryAdd()));

        Assert.Single(added, x => x);
        await using var check = fixture.CreateDb();
        Assert.Equal(1, await check.LegacyOrders.IgnoreQueryFilters().CountAsync(o => o.CustomerBill == key, Ct));
    }
}

public sealed class DraftSqlServerFixture : IAsyncLifetime
{
    public const long CustomerId = 42;
    private const string Prefix = "vietan_tests_drafts_";
    private readonly string databaseName = Prefix + Guid.NewGuid().ToString("N");
    private string? connectionString;

    internal ShipmentsDbContext CreateDb() => new(new DbContextOptionsBuilder<ShipmentsDbContext>()
        .UseSqlServer(connectionString ?? throw new InvalidOperationException("VIETAN_TEST_SQLSERVER is required"),
            sql => sql.EnableRetryOnFailure(3))
        .Options);

    /// <summary>1 Ä‘Æ¡n nhÃ¡p Ä‘á»§ thÃ´ng tin Ä‘á»ƒ in (Status = 0, chÆ°a cÃ³ sá»‘ bill).</summary>
    internal async Task<long> AddReadyDraftAsync()
    {
        await using var db = CreateDb();
        var draft = new LegacyOrder
        {
            CustomerId = CustomerId, Status = LegacyOrder.DraftStatus, VaBill = "VAHCM",
            SenderName = "Shop A", ConsigneeName = "Receiver", ConsigneeCountry = "Singapore", ConsigneeCity = "Singapore",
            ConsigneeAddress1 = "1 Test Road", ConsigneePostalCode = "123456", ServiceName = "DHL|Singapore",
            WeightKg = 1.5m, Pieces = 1, GoodsName = "Clothes", Currency = "USD"
        };
        db.LegacyOrders.Add(draft);
        await db.SaveChangesAsync();
        return draft.Id;
    }

    public async ValueTask InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server) { InitialCatalog = databaseName }.ConnectionString;
        await using var schema = new TestSchemaDb(new DbContextOptionsBuilder<TestSchemaDb>().UseSqlServer(connectionString).Options);
        await schema.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (connectionString is null) return;
        // Chá»‰ xoÃ¡ Ä‘Ãºng database táº¡m do fixture nÃ y táº¡o.
        if (!databaseName.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(databaseName[Prefix.Length..], "N", out _))
            throw new InvalidOperationException("Invalid disposable database name");
        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        await using var schema = new TestSchemaDb(new DbContextOptionsBuilder<TestSchemaDb>().UseSqlServer(connectionString).Options);
        await schema.Database.EnsureDeletedAsync();
        connectionString = null;
    }

    // Chá»‰ trong database táº¡m: cho phÃ©p táº¡o 3 báº£ng váº­n Ä‘Æ¡n cá»§a há»‡ thá»‘ng cÅ©.
    private sealed class TestSchemaDb(DbContextOptions<TestSchemaDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new LegacyOrderConfiguration());
            modelBuilder.ApplyConfiguration(new LegacyPackageLineConfiguration());
            modelBuilder.ApplyConfiguration(new LegacyInvoiceLineConfiguration());
            modelBuilder.Entity<LegacyOrder>().ToTable("MaVanDon", "dbo", t => t.ExcludeFromMigrations(false));
            modelBuilder.Entity<LegacyPackageLine>().ToTable("MaVanDon_PCS_DIM", "dbo", t => t.ExcludeFromMigrations(false));
            modelBuilder.Entity<LegacyInvoiceLine>().ToTable("MaVanDon_ChiTietHang", "dbo", t => t.ExcludeFromMigrations(false));
        }
    }
}
