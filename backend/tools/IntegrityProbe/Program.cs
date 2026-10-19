using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Http;
using VietAnExpress.SharedKernel.Web;

// Diagnostic only: reproduces interleaved requests against real EF mappings in
// an isolated database. No application data, external HTTP, or email is used.
if (args.Length == 2 && args[0] == "--export-portal-ddl")
{
    Directory.CreateDirectory(args[1]);
    foreach (var module in new[] { "Shipments", "Ecommerce", "Identity" })
    {
        await using var export = new ProbeDb("Server=(local);Database=unused;Integrated Security=True;TrustServerCertificate=True", module, includeExcluded: false);
        await File.WriteAllTextAsync(Path.Combine(args[1], module + ".create.sql"), export.Database.GenerateCreateScript());
        Console.WriteLine("Exported final portal DDL: " + module);
    }
    return;
}
var server = Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER")
    ?? throw new InvalidOperationException("Set VIETAN_TEST_SQLSERVER to a disposable-test SQL Server connection.");
var database = "vietan_tests_integrity_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder(server) { InitialCatalog = "master" };
await using var master = new SqlConnection(connection.ConnectionString);
await master.OpenAsync();
await using (var create = master.CreateCommand())
{
    create.CommandText = $"CREATE DATABASE [{database}]";
    await create.ExecuteNonQueryAsync();
}
connection.InitialCatalog = database;
var results = new List<object>();
var modelDifferences = new List<object>();
ProbeDb Open(string module) => new(connection.ConnectionString, module);
void Record(string name, object evidence)
{
    results.Add(new { Name = name, Evidence = evidence });
    Console.WriteLine($"PROBE {name}: {JsonSerializer.Serialize(evidence)}");
}
object Order(ProbeDb db, string receiver = "Original")
{
    var order = Activator.CreateInstance(db.EntityType("LegacyOrder"), nonPublic: true)!;
    Set(order, "CustomerId", 42L); Set(order, "Status", 0); Set(order, "ConsigneeEmail", "");
    Set(order, "ConsigneeName", receiver); Set(order, "VaBill", "VAHN");
    return order;
}
try
{
    foreach (var module in new[] { "Shipments", "Identity" })
    {
        await using var db = Open(module);
        var ddl = db.Database.GenerateCreateScript();
        foreach (var batch in Regex.Split(ddl, @"(?m)^\s*GO\s*$").Where(s => !string.IsNullOrWhiteSpace(s)))
            await db.Database.ExecuteSqlRawAsync(batch);
    }

    // Both requests have loaded the draft before either request commits.
    await using (var first = Open("Shipments"))
    await using (var second = Open("Shipments"))
    {
        var seeded = Order(first); first.Add(seeded); await first.SaveChangesAsync();
        var id = Get(seeded, "Id");
        var stale = await Load(second, seeded.GetType(), id);
        Set(seeded, "OrderNumber", 6000001L); Set(seeded, "Status", 1); Set(seeded, "VaBill", "VAHN6000001SG");
        await first.SaveChangesAsync();
        Set(stale, "OrderNumber", 6000002L); Set(stale, "Status", 1); Set(stale, "VaBill", "VAHN6000002SG");
        await second.SaveChangesAsync();
        first.ChangeTracker.Clear();
        var final = await Load(first, seeded.GetType(), id);
        Record("Two print requests overwrite the first bill", new { FirstBill = 6000001L, StoredBill = Get(final, "OrderNumber"), ConcurrencyProperties = first.Model.FindEntityType(seeded.GetType())!.GetProperties().Where(p => p.IsConcurrencyToken).Select(p => p.Name).ToArray() });
    }
    await using (var first = Open("Shipments"))
    await using (var second = Open("Shipments"))
    {
        var seeded = Order(first); first.Add(seeded); await first.SaveChangesAsync();
        var stale = await Load(second, seeded.GetType(), Get(seeded, "Id"));
        Set(seeded, "OrderNumber", 6000003L); Set(seeded, "Status", 1); await first.SaveChangesAsync();
        Set(stale, "ConsigneeName", "Changed after bill issued"); await second.SaveChangesAsync();
        first.ChangeTracker.Clear();
        var final = await Load(first, seeded.GetType(), Get(seeded, "Id"));
        Record("Stale draft edit modifies an issued order", new { Status = Get(final, "Status"), Receiver = Get(final, "ConsigneeName") });
    }
    await using (var first = Open("Shipments"))
    await using (var second = Open("Shipments"))
    {
        var seeded = Order(first); first.Add(seeded); await first.SaveChangesAsync();
        var stale = await Load(second, seeded.GetType(), Get(seeded, "Id"));
        Set(seeded, "OrderNumber", 6000004L); Set(seeded, "Status", 1); await first.SaveChangesAsync();
        second.Remove(stale); var deleted = await second.SaveChangesAsync();
        Record("Stale draft delete removes an issued order", new { RowsDeleted = deleted });
    }
    await using (var first = Open("Identity"))
    await using (var second = Open("Identity"))
    {
        var type = first.EntityType("MyTrackingPage");
        first.Add(Activator.CreateInstance(type, 42L, "same-slug", "{}", true, DateTime.Now)!);
        second.Add(Activator.CreateInstance(type, 43L, "same-slug", "{}", true, DateTime.Now)!);
        await first.SaveChangesAsync();
        try { await second.SaveChangesAsync(); Record("MyTracking duplicate slug", new { Rejected = false }); }
        catch (DbUpdateException ex)
        {
            Record("MyTracking duplicate slug", new { Rejected = true, SqlError = (ex.InnerException as SqlException)?.Number, HttpStatus = ProblemDetailsMapper.FromException(new DefaultHttpContext(), ex).Status });
        }
    }
    await using (var first = Open("Identity"))
    await using (var second = Open("Identity"))
    {
        var type = first.EntityType("StaffAccount");
        first.Add(Activator.CreateInstance(type, 42L, "same-user", "TestOnly123", "First", null, null, Array.Empty<string>(), DateTime.Now)!);
        second.Add(Activator.CreateInstance(type, 43L, "same-user", "TestOnly123", "Second", null, null, Array.Empty<string>(), DateTime.Now)!);
        await first.SaveChangesAsync(); await second.SaveChangesAsync();
        Record("Concurrent staff creation accepts duplicate usernames", new { RowsWithSameUsername = await first.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.TCustomer_NguoiLienLac WHERE TinhCach=N'same-user'").SingleAsync() });
    }
    await using (var first = Open("Identity"))
    await using (var second = Open("Identity"))
    {
        var type = first.EntityType("MyTrackingPage");
        var id = await first.Database.SqlQuery<int>($"SELECT ID AS [Value] FROM dbo.TCustomer_MyTracking WHERE CustomerID=42").SingleAsync();
        var pageA = await Load(first, type, id);
        var pageB = await Load(second, type, id);
        var update = type.GetMethod("Update")!;
        update.Invoke(pageA, ["same-slug", """{"images":[{"id":"a","src":"https://example.test/a.png"}]}""", true, DateTime.Now]);
        update.Invoke(pageB, ["same-slug", """{"images":[{"id":"b","src":"https://example.test/b.png"}]}""", true, DateTime.Now]);
        await first.SaveChangesAsync(); await second.SaveChangesAsync();
        Record("Concurrent MyTracking saves mix two image sets", new { ImagesSubmittedPerRequest = 1, StoredImages = await first.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.TCustomer_MyTracking_Banner WHERE MyTrackingID={id}").SingleAsync() });
    }
    await using (var db = Open("Identity"))
    {
        // Match nullable legacy SQL columns; model currently declares them required.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.TCustomer_NguoiLienLac ALTER COLUMN Mo_Ta nvarchar(150) NULL; ALTER TABLE dbo.TCustomer_NguoiLienLac ALTER COLUMN Quyen nvarchar(1000) NULL; ALTER TABLE dbo.TCustomer_NguoiLienLac ALTER COLUMN Ten nvarchar(150) NULL;");
        var id = await db.Database.SqlQuery<long>($"INSERT dbo.TCustomer_NguoiLienLac (CustomerID,TinhCach,Chuc_Vu,Phong_Ban,Trang_Thai,Create_Date) OUTPUT INSERTED.ID AS [Value] VALUES (42,N'legacy-null',0,0,1,GETDATE())").ToListAsync();
        try { await Load(db, db.EntityType("StaffAccount"), id[0]); Record("Legacy staff with NULL fields", new { ReadSucceeded = true }); }
        catch (Exception ex) { Record("Legacy staff with NULL fields", new { ReadSucceeded = false, Exception = ex.GetType().Name }); }
    }
    await using (var first = Open("Shipments"))
    await using (var second = Open("Shipments"))
    {
        var type = first.EntityType("CatalogMark");
        var factory = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;
        first.Add(factory.Invoke(null, [42L, "SP", "test-key", DateTime.Now])!);
        second.Add(factory.Invoke(null, [42L, "SP", "test-key", DateTime.Now])!);
        await first.SaveChangesAsync();
        try { await second.SaveChangesAsync(); Record("Concurrent catalog upsert", new { Rejected = false }); }
        catch (DbUpdateException ex) { Record("Concurrent catalog upsert", new { Rejected = true, SqlError = (ex.InnerException as SqlException)?.Number, HttpStatus = ProblemDetailsMapper.FromException(new DefaultHttpContext(), ex).Status }); }
    }
    // Exercise the exact allocation query used by the portal, holding its lock
    // through the order insert and commit, with separate connections.
    var allocated = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
    {
        await using var db = Open("Shipments");
        await using var tx = await db.Database.BeginTransactionAsync();
        var maximum = await db.Database.SqlQuery<long?>($"SELECT MAX(OrderNumber) AS [Value] FROM dbo.MaVanDon WITH (UPDLOCK, HOLDLOCK) WHERE OrderNumber < {10000000L}").SingleAsync();
        var number = (maximum ?? 0) + 1;
        var order = Order(db); Set(order, "Status", 1); Set(order, "OrderNumber", number);
        db.Add(order); await db.SaveChangesAsync(); await tx.CommitAsync();
        return number;
    }));
    Record("Twenty simultaneous allocations to separate orders", new { Requests = allocated.Length, DistinctNumbers = allocated.Distinct().Count() });
    if (args.Length > 1)
    {
        using var audit = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
        var actual = audit.RootElement.GetProperty("Columns").EnumerateArray().ToArray();
        foreach (var module in new[] { "Identity", "Shipments", "Customers", "Ecommerce" })
        {
            await using var db = Open(module);
            foreach (var table in db.Model.GetRelationalModel().Tables)
            foreach (var column in table.Columns)
            {
                var match = actual.FirstOrDefault(c => c.GetProperty("Schema").GetString() == table.Schema && c.GetProperty("Table").GetString() == table.Name && c.GetProperty("Column").GetString() == column.Name);
                if (match.ValueKind == JsonValueKind.Undefined) { modelDifferences.Add(new { Module = module, Table = table.Name, Column = column.Name, Missing = true }); continue; }
                var sqlType = match.GetProperty("Type").GetString()!;
                if (sqlType is "nvarchar" or "varchar" or "char" or "nchar")
                {
                    var bytes = match.GetProperty("MaxBytes").GetInt32();
                    sqlType += "(" + (bytes == -1 ? "max" : (bytes / (sqlType.StartsWith('n') ? 2 : 1)).ToString()) + ")";
                }
                // Decimal precision is intentionally omitted from this metadata comparison.
                var typeMismatch = !column.StoreType.StartsWith("decimal", StringComparison.OrdinalIgnoreCase) && !string.Equals(sqlType, column.StoreType, StringComparison.OrdinalIgnoreCase);
                if (typeMismatch || column.IsNullable != match.GetProperty("Nullable").GetBoolean())
                    modelDifferences.Add(new { Module = module, Table = table.Name, Column = column.Name, ModelType = column.StoreType, SqlType = sqlType, ModelNullable = column.IsNullable, SqlNullable = match.GetProperty("Nullable").GetBoolean() });
            }
        }
    }
}
finally
{
    if (!database.StartsWith("vietan_tests_integrity_", StringComparison.Ordinal) || !Guid.TryParseExact(database[23..], "N", out _))
        throw new InvalidOperationException("Invalid disposable database name");
    SqlConnection.ClearAllPools();
    await using var drop = master.CreateCommand();
    drop.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];";
    await drop.ExecuteNonQueryAsync();
    Console.WriteLine("Disposable database removed.");
}
if (args.Length > 0)
    await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new { Results = results, ModelDifferences = modelDifferences }, new JsonSerializerOptions { WriteIndented = true }));

static object Get(object entity, string property) => entity.GetType().GetProperty(property)!.GetValue(entity)!;
static void Set(object entity, string property, object? value) => entity.GetType().GetProperty(property)!.SetValue(entity, value);
static Task<object> Load(DbContext db, Type type, object id) => (Task<object>)typeof(ProbeQueries).GetMethod(nameof(ProbeQueries.Load))!.MakeGenericMethod(type).Invoke(null, [db, id])!;

public static class ProbeQueries
{
    public static async Task<object> Load<T>(DbContext db, object id) where T : class => id is int key
        ? await db.Set<T>().IgnoreQueryFilters().SingleAsync(e => EF.Property<int>(e, "Id") == key)
        : await db.Set<T>().IgnoreQueryFilters().SingleAsync(e => EF.Property<long>(e, "Id") == (long)id);
}
public sealed class ProbeDb(string connection, string module, bool includeExcluded = true) : DbContext
{
    public string Module => module;
    public bool IncludeExcluded => includeExcluded;
    private Assembly Assembly => Assembly.Load("VietAnExpress." + module);
    public Type EntityType(string name) => Assembly.GetTypes().Single(t => t.Name == name);
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlServer(connection).ReplaceService<IModelCacheKeyFactory, ProbeCacheKey>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(module.ToLowerInvariant());
        if (module == "Shipments")
            model.HasSequence<long>("LegacyOrderNumberSequence").StartsAt(90000001);
        model.ApplyConfigurationsFromAssembly(Assembly);
        if (includeExcluded)
            foreach (var entity in model.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToArray())
                model.Entity(entity.ClrType).ToTable(entity.GetTableName()!, entity.GetSchema(), table => table.ExcludeFromMigrations(false));
    }
}
public sealed class ProbeCacheKey : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => (context.GetType(), ((ProbeDb)context).Module, ((ProbeDb)context).IncludeExcluded, designTime);
}
