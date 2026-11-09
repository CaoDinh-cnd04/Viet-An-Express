using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;
using VietAnExpress.Shipments.Tracking;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

// Run against a disposable database, never the application database.
public class OrderQuerySqlServerTests(SqlServerQueryFixture fixture) : IClassFixture<SqlServerQueryFixture>
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER"));
    private static readonly DateTime Today = new(2026, 10, 3);
    private static readonly TimeProvider Clock = new FixedClock();

    private static OrderAccess Access(ShipmentsDbContext db, long? customerId = 42)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.CustomerId).Returns(customerId);
        return new OrderAccess(user.Object, Mock.Of<ICustomersApi>());
    }

    private static GetOrdersQuery Query(string? status = null, int page = 1, int size = 3) =>
        new(null, null, status, null, null, null, null, null, page, size, "created", "desc");

    [Theory(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    [InlineData(null)]
    [InlineData("wait")]
    [InlineData("fly")]
    [InlineData("nd")]
    [InlineData("ok")]
    [InlineData("wait,ok,wait")]
    [InlineData("fly,ok")]
    [InlineData("wait,fly,nd,ok")]
    [InlineData("unknown")]
    public async Task List_matches_existing_counts_totals_and_DTOs_using_two_SQL_reads(string? status)
    {
        var commands = new ReadCounter();
        await using var db = fixture.CreateDb(commands);
        var q = Query(status);
        var scope = db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 42);

        // Baseline: 1 lần đếm tất cả + 1 lần mỗi trạng thái + tổng kiện/cân + tổng dòng + trang dữ liệu.
        var counts = new Dictionary<string, int> { ["all"] = await scope.CountAsync(TestContext.Current.CancellationToken) };
        foreach (var code in LegacyOrderStatus.All)
            counts[code] = await scope.CountAsync(LegacyOrderStatus.Is(code, Today), TestContext.Current.CancellationToken);
        var filtered = OrderListFilter.ApplyStatus(scope, status, Today);
        // Tổng kiện / cân theo đúng trạng thái đang lọc (khớp danh sách đang xem).
        var sums = await filtered.GroupBy(_ => 1)
            .Select(g => new { Pieces = g.Sum(o => o.Pieces ?? 0), Weight = g.Sum(o => o.WeightKg ?? 0) })
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken) ?? new { Pieces = 0, Weight = 0m };
        var expectedTotal = await filtered.CountAsync(TestContext.Current.CancellationToken);
        var expectedItems = (await OrderListFilter.Sort(filtered, q.SortBy, q.SortDir).Take(3).ToListAsync(TestContext.Current.CancellationToken))
            .Select(o => LegacyOrderView.ToListDto(o, Today)).ToList();
        Assert.Equal(4 + LegacyOrderStatus.All.Length, commands.Reads.Count);
        commands.Reads.Clear();

        var response = await new GetOrdersHandler(db, Access(db), Clock).Handle(q, TestContext.Current.CancellationToken);
        // 1 lần gom nhóm trạng thái (mọi tổ hợp tab tính ở bộ nhớ) + 1 lần lấy trang
        Assert.Equal(2, commands.Reads.Count);
        Assert.Equal(expectedTotal, response.Total);
        Assert.Equal((int)Math.Ceiling(expectedTotal / 3d), response.TotalPages);
        foreach (var pair in counts) Assert.Equal(pair.Value, response.Summary.StatusCounts[pair.Key]);
        Assert.Equal(sums.Pieces, response.Summary.TotalPieces);
        Assert.Equal(sums.Weight, response.Summary.TotalWeight);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(response.Items));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.DoesNotContain("[SenderEmail]", commands.Reads[^1]);
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Search_date_weight_and_page_filters_preserve_the_matching_set()
    {
        var commands = new ReadCounter();
        await using var db = fixture.CreateDb(commands);
        var q = Query("wait,fly", page: 2, size: 2) with
        {
            Q = "ct:Singapore", FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 10, 3), WeightFrom = 1m, WeightTo = 20m
        };
        var scope = OrderListFilter.Apply(db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 42), q);
        var expectedAll = await scope.CountAsync(TestContext.Current.CancellationToken);
        var filtered = OrderListFilter.ApplyStatus(scope, q.Status, Today);
        var expectedTotal = await filtered.CountAsync(TestContext.Current.CancellationToken);
        var expectedIds = await OrderListFilter.Sort(filtered, q.SortBy, q.SortDir).Skip(2).Take(2).Select(o => o.Id).ToListAsync(TestContext.Current.CancellationToken);
        commands.Reads.Clear();
        var response = await new GetOrdersHandler(db, Access(db), Clock).Handle(q, TestContext.Current.CancellationToken);
        Assert.Equal(expectedTotal, response.Total);
        Assert.Equal(expectedAll, response.Summary.StatusCounts["all"]);
        Assert.Equal(expectedIds, response.Items.Select(o => o.Seq));
        // Có bộ lọc, kết quả ít: 1 lượt quét dòng gọn (đếm + sắp xếp + phân trang ở bộ nhớ) + 1 lần đọc đúng trang theo ID
        Assert.Equal(expectedIds.Count > 0 ? 2 : 1, commands.Reads.Count);
    }

    [Theory(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    [InlineData(42L, int.MaxValue)]
    [InlineData(999L, 1)]
    [InlineData(null, 1)]
    public async Task Empty_or_out_of_range_pages_need_only_one_read(long? customerId, int page)
    {
        var commands = new ReadCounter();
        await using var db = fixture.CreateDb(commands);
        var response = await new GetOrdersHandler(db, Access(db, customerId), Clock).Handle(Query(page: page, size: 100), TestContext.Current.CancellationToken);
        Assert.Empty(response.Items);
        Assert.Single(commands.Reads);
        if (customerId != 42)
        {
            Assert.Equal(0, response.Total);
            Assert.Equal(0, response.Summary.TotalWeight);
            Assert.All(response.Summary.StatusCounts.Values, n => Assert.Equal(0, n));
        }
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Recent_invoices_keep_customer_scope_limit_item_order_and_currency_with_two_reads()
    {
        var commands = new ReadCounter();
        await using var db = fixture.CreateDb(commands);
        var orders = await db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 42).ToDictionaryAsync(o => o.Id, TestContext.Current.CancellationToken);
        var lines = await db.LegacyInvoiceLines.AsNoTracking().Where(l => l.OrderId != null && orders.Keys.Contains((long)l.OrderId.Value))
            .OrderByDescending(l => l.OrderId).ThenBy(l => l.Id).ToListAsync(TestContext.Current.CancellationToken);
        var expected = ProductLibrary.Recent(lines.Select(l => new CustomerInvoiceLine(l, orders[l.OrderId!.Value])), 2);
        commands.Reads.Clear();
        var actual = await new GetRecentInvoicesHandler(db, Access(db)).Handle(new GetRecentInvoicesQuery(2), TestContext.Current.CancellationToken);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(2, commands.Reads.Count);
        Assert.DoesNotContain("[SenderEmail]", commands.Reads[0]);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Public_tracking_keeps_all_bill_identifiers_and_result_fields_with_one_read()
    {
        var commands = new ReadCounter();
        await using var db = fixture.CreateDb(commands);
        var full = await db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 42 && o.OrderNumber != null)
            .OrderBy(o => o.Id).FirstAsync(TestContext.Current.CancellationToken);
        string[] bills = [full.OrderNumber!.Value.ToString(), full.BillConnect!, full.Awb!, full.CustomerBill!];
        commands.Reads.Clear();
        var result = await new LegacyBillReader(db, NullLogger<LegacyBillReader>.Instance).FindAsync(bills, TestContext.Current.CancellationToken);
        var projected = Assert.Single(result);
        Assert.Single(commands.Reads);
        foreach (var bill in bills)
        {
            Assert.True(LegacyTrackingMapper.Matches(projected, bill));
            Assert.Equal(JsonSerializer.Serialize(LegacyTrackingMapper.ToResult(bill, full, Today)),
                JsonSerializer.Serialize(LegacyTrackingMapper.ToResult(bill, projected, Today)));
        }
        Assert.DoesNotContain("[ConsigneeEmail]", commands.Reads[0]);
        Assert.DoesNotContain("[ConsigneeName]", commands.Reads[0]);
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Unknown_carrier_bill_does_not_match_an_order_with_a_null_order_number()
    {
        await using var db = fixture.CreateDb(new ReadCounter());
        var result = await new GetOrderEventsHandler(db, Access(db), NoHubEz.Instance, NoOrderTracking.Instance)
            .Handle(new GetOrderEventsQuery("NONEXISTENT-CARRIER-BILL"), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("ORDER_NOT_FOUND", result.Error.Code);
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Events_project_only_event_fields_and_enforce_customer_scope()
    {
        var commands = new ReadCounter();
        await using var db = fixture.CreateDb(commands);
        var full = await db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 42).OrderBy(o => o.Id).FirstAsync(TestContext.Current.CancellationToken);
        commands.Reads.Clear();
        var handler = new GetOrderEventsHandler(db, Access(db), NoHubEz.Instance, NoOrderTracking.Instance);
        var result = await handler.Handle(new GetOrderEventsQuery(full.BillConnect!), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(JsonSerializer.Serialize(LegacyOrderView.Events(full, hideSigner: false)), JsonSerializer.Serialize(result.Value));
        Assert.Single(commands.Reads);
        Assert.DoesNotContain("[ConsigneeEmail]", commands.Reads[0]);
        var denied = await handler.Handle(new GetOrderEventsQuery("OTHER-CUSTOMER"), TestContext.Current.CancellationToken);
        Assert.True(denied.IsFailure);
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Managed_orders_cover_only_customers_of_the_agent_and_attach_customer()
    {
        await using var db = fixture.CreateDb(new ReadCounter());
        var ct = TestContext.Current.CancellationToken;
        var customers = new Mock<ICustomersApi>();
        customers.Setup(c => c.GetVietAnUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new VietAnUserLogin(7, "nv", "NV", false, "x"));
        customers.Setup(c => c.GetCustomerIdsOfManagerAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync([43L]);
        customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CustomerSummary(43, "KH43", "Công ty 43", null, null, null, null, null)]);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.CustomerId).Returns(42); // đang chọn khách 42 — trang phụ trách không phụ thuộc khách đang chọn
        user.SetupGet(u => u.VietAnUserId).Returns(7);
        var access = new OrderAccess(user.Object, customers.Object);

        var managed = await new GetOrdersHandler(db, access, Clock).Handle(Query(size: 50) with { Managed = true }, ct);
        var item = Assert.Single(managed.Items);
        Assert.Equal(new OrderCustomerDto("43", "KH43", "Công ty 43"), item.Customer);
        Assert.Equal(1, managed.Summary.StatusCounts["all"]);

        // Tìm 1 đơn (chi tiết / hành trình dùng chung OrderLookup) theo phạm vi phụ trách.
        var events = new GetOrderEventsHandler(db, access, NoHubEz.Instance, NoOrderTracking.Instance);
        Assert.True((await events.Handle(new GetOrderEventsQuery("OTHER-CUSTOMER", Managed: true), ct)).IsSuccess);
        // Đơn của khách 42 (đang chọn, nhưng không do nhân viên phụ trách) không có ở trang phụ trách.
        Assert.True((await events.Handle(new GetOrderEventsQuery("CARRIER-1", Managed: true), ct)).IsFailure);
        Assert.True((await events.Handle(new GetOrderEventsQuery("CARRIER-1"), ct)).IsSuccess);
        // Tìm theo khách (thẻ "kh"): khớp mã / tên khách qua module Customers.
        customers.Setup(c => c.SearchCustomersAsync(It.IsAny<string?>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        customers.Setup(c => c.SearchCustomersAsync("KH43", null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CustomerSummary(43, "KH43", "Công ty 43", null, null, null, null, null)]);
        var byCustomer = await new GetOrdersHandler(db, access, Clock).Handle(Query(size: 50) with { Q = "kh:KH43", Managed = true }, ct);
        Assert.Single(byCustomer.Items);
        var noMatch = await new GetOrdersHandler(db, access, Clock).Handle(Query(size: 50) with { Q = "kh:không có", Managed = true }, ct);
        Assert.Empty(noMatch.Items);

        // "Đơn hàng của tôi" giữ nguyên phạm vi khách đang chọn, không gắn khách.
        var mine = await new GetOrdersHandler(db, access, Clock).Handle(Query(size: 50), ct);
        Assert.All(mine.Items, o => Assert.Null(o.Customer));
        Assert.DoesNotContain(mine.Items, o => o.Cnee == "Other customer");
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Search_ignores_case_and_vietnamese_accents_on_search_fields()
    {
        await using var db = fixture.CreateDb(new ReadCounter());
        var ct = TestContext.Current.CancellationToken;
        var order = new LegacyOrder
        {
            CustomerId = 44, OrderNumber = 91000001, ConsigneeName = "Công ty Đức Thịnh", ConsigneeContactName = "Nguyễn Văn Bình",
            ConsigneeCity = "Đà Nẵng", ConsigneePhone = "0905 123 456", ConsigneeAddress1 = "12 Lê Lợi", GoodsName = "Xoài sấy dẻo",
            ServiceName = "Chuyên tuyến|Singapore", ConsigneeCountry = "Singapore", CreateDate = new(2026, 10, 1)
        };
        db.LegacyOrders.Add(order);
        await db.SaveChangesAsync(ct);
        try
        {
            var user = new Mock<ICurrentUser>();
            user.SetupGet(u => u.CustomerId).Returns(44);
            var handler = new GetOrdersHandler(db, new OrderAccess(user.Object, Mock.Of<ICustomersApi>()), Clock);
            async Task<int> Count(string q) => (await handler.Handle(Query(size: 50) with { Q = q }, ct)).Total;

            foreach (var q in new[] { "duc thinh", "CÔNG TY", "nguyen van", "đức THỊNH", "singapore", "chuyen tuyen - singapore",
                         "svc:chuyen tuyen", "cnee:binh", "ct:SINGAPORE", "9100" })
                Assert.True(await Count(q) == 1, q);
            // "Tất cả" = các trường của ô chọn (06/11/2026): không tìm thành phố / tên hàng / địa chỉ / SĐT nữa.
            foreach (var q in new[] { "hanoi", "da nang", "xoai say", "le loi", "123 456" })
                Assert.True(await Count(q) == 0, q);
        }
        finally
        {
            db.LegacyOrders.Remove(order);
            await db.SaveChangesAsync(ct);
        }
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Loc_DOC_bang_SQL_khop_cot_Loai_hien_thi()
    {
        await using var db = fixture.CreateDb(new ReadCounter());
        var ct = TestContext.Current.CancellationToken;
        string[] names = ["Documents", "DOC", "doc", "docs", "Doc giấy", "giấy docs", "hồ sơ docs xin visa", "Chứng từ", "CHỨNG TỪ gốc",
            "Hồ sơ", "document", "Doctor bag", "docx", "Quần áo", "adoc", "doc.", "Giấy tờ", "chung tu"];
        var orders = names.Select((n, i) => new LegacyOrder { CustomerId = 45, OrderNumber = 92000000 + i, GoodsName = n }).ToList();
        db.LegacyOrders.AddRange(orders);
        await db.SaveChangesAsync(ct);
        try
        {
            var sql = await db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 45).Where(LegacyDocumentRule.IsDocument)
                .Select(o => o.GoodsName!).ToListAsync(ct);
            Assert.Equal(names.Where(LegacyDocumentRule.Matches).Order(), sql.Order());
            var pack = await db.LegacyOrders.AsNoTracking().Where(o => o.CustomerId == 45).Where(LegacyDocumentRule.IsNotDocument).CountAsync(ct);
            Assert.Equal(names.Count(n => !LegacyDocumentRule.Matches(n)), pack);
        }
        finally
        {
            db.LegacyOrders.RemoveRange(orders);
            await db.SaveChangesAsync(ct);
        }
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to run disposable SQL Server integration tests", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Dan_nhieu_bill_tim_dung_so_VA_ma_VA_va_ma_hang()
    {
        await using var db = fixture.CreateDb(new ReadCounter());
        var ct = TestContext.Current.CancellationToken;
        var handler = new GetOrdersHandler(db, Access(db), Clock);
        // Fixture khách 42: OrderNumber 90000000 + i, mã hãng "CARRIER-" + i (i = 2 không có số bill → không hiện).
        // Mã VA thật 7 chữ số (VAHCM6003585US): đơn mẫu 8 chữ số nên mã VA của nó không đọc ra số → so thẳng VA_Bill (để trống) → không khớp.
        var q = string.Join((char)10, "90000001", "VAHCM90000003SG", "90000004/2", "carrier-5", "khong-co");
        var r = await handler.Handle(Query(size: 50) with { Q = q, SearchField = "bill" }, ct);
        Assert.Equal([90000005L, 90000004, 90000001], r.Items.Select(o => long.Parse(o.Bill)));
        Assert.Equal(3, r.Total);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 3, 5, 0, 0, TimeSpan.Zero);
    }
}

public sealed class SqlServerQueryFixture : IAsyncLifetime
{
    private readonly string databaseName = "vietan_tests_queries_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    internal ShipmentsDbContext CreateDb(ReadCounter counter) => new(new DbContextOptionsBuilder<ShipmentsDbContext>()
        .UseSqlServer(connectionString ?? throw new InvalidOperationException("VIETAN_TEST_SQLSERVER is required"))
        .AddInterceptors(counter).Options);

    public async ValueTask InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server) { InitialCatalog = databaseName };
        connectionString = builder.ConnectionString;
        try
        {
            await using var schema = new TestSchemaDb(new DbContextOptionsBuilder<TestSchemaDb>().UseSqlServer(connectionString).Options);
            await schema.Database.EnsureCreatedAsync();
            var rows = new List<LegacyOrder>();
            string?[] pods = [null, "", "Address unavailable", "Delivery attempted", "03/10/2026 08:30, DELIVERED TEST", " "];
            foreach (var pod in pods)
            foreach (var eta in new DateTime?[] { null, new(2026, 10, 2), new(2026, 10, 3) })
            foreach (var sent in new DateTime?[] { null, new(2026, 10, 1) })
            {
                var i = rows.Count + 1;
                rows.Add(new LegacyOrder
                {
                    CustomerId = 42, OrderNumber = i == 2 ? null : 90000000 + i,
                    BillConnect = "CARRIER-" + i, Awb = "AWB-" + i, CustomerBill = "REF-" + i,
                    Pod = pod, PodEstimate = eta, SentDate = sent, CreateDate = new(2026, 10, i % 3 + 1),
                    Pieces = i % 4 == 0 ? null : 2, WeightKg = i % 4 == 0 ? null : i / 2m,
                    GoodsName = i % 2 == 0 ? "Documents" : "Clothes", Currency = "SGD",
                    ConsigneeName = " Test Receiver ", ConsigneeContactName = "Contact", ConsigneePhone = "0000000000",
                    ConsigneeCountry = "Singapore", ConsigneeCity = "Singapore", ConsigneeState = "State",
                    ConsigneePostalCode = "123456", ConsigneeEmail = "receiver@example.test", ConsigneeAddress1 = "Address1",
                    ConsigneeAddress2 = "Address2", ConsigneeAddress3 = "Address3", ConsigneeVatTax = "Tax",
                    ConsigneeIossNo = "IM1234567890", ConsigneeEoriNo = "EORI", ServiceName = "DHL|Singapore",
                    SenderCountryId = 231, SenderEmail = "sender@example.test", GoodsValue = 123m
                });
            }
            rows.Add(new LegacyOrder { CustomerId = 43, OrderNumber = 99000043, BillConnect = "OTHER-CUSTOMER", ConsigneeName = "Other customer", Pieces = 999, WeightKg = 999 });
            schema.AddRange(rows);
            await schema.SaveChangesAsync();
            foreach (var row in rows.Where(o => o.Id % 3 == 0 || o.CustomerId == 43))
            {
                schema.Add(new LegacyInvoiceLine { OrderId = (int)row.Id, DescriptionEn = "First item", Quantity = 1, UnitPrice = 4.5m, Currency = "" });
                schema.Add(new LegacyInvoiceLine { OrderId = (int)row.Id, DescriptionEn = "Second item", Quantity = 2, UnitPrice = 10m, Currency = "EUR" });
            }
            schema.Add(new LegacyInvoiceLine { OrderId = null, DescriptionEn = "Orphan line" });
            await schema.SaveChangesAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connectionString is null) return;
        // Only this fixture's generated database can ever be deleted.
        if (!databaseName.StartsWith("vietan_tests_queries_", StringComparison.Ordinal)
            || !Guid.TryParseExact(databaseName["vietan_tests_queries_".Length..], "N", out _))
            throw new InvalidOperationException("Invalid disposable database name");
        await using var schema = new TestSchemaDb(new DbContextOptionsBuilder<TestSchemaDb>().UseSqlServer(connectionString).Options);
        await schema.Database.EnsureDeletedAsync();
        connectionString = null;
    }

    // Enable legacy table creation only in this temporary test database.
    private sealed class TestSchemaDb(DbContextOptions<TestSchemaDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new LegacyOrderConfiguration());
            modelBuilder.ApplyConfiguration(new LegacyInvoiceLineConfiguration());
            modelBuilder.Entity<LegacyOrder>().ToTable("MaVanDon", "dbo", t => t.ExcludeFromMigrations(false));
            modelBuilder.Entity<LegacyInvoiceLine>().ToTable("MaVanDon_ChiTietHang", "dbo", t => t.ExcludeFromMigrations(false));
        }
    }
}

internal sealed class ReadCounter : DbCommandInterceptor
{
    public List<string> Reads { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Reads.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
