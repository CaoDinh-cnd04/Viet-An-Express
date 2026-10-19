using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.Identity.Infrastructure.Migrations;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class MyTrackingMigrationSqlServerTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Migration_keeps_full_configuration_and_assets_before_dropping_old_table()
    {
        await WithDatabase(async db =>
        {
            var config = MyTrackingStorageTests.FullConfig();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.MyTrackingCauHinh (CustomerID,Duong_Dan,Cau_Hinh,Da_Xuat_Ban,CreateDate,ModifyDate)
                VALUES (42,'keep-this-url',{config.ToJsonString()},1,'2026-10-01','2026-10-02');
                INSERT dbo.TCustomer_MyTracking (CustomerID,CompanyName,Color_01) VALUES (99,N'Existing legacy company',N'#abcdef');
                """, Ct);
            await ApplyAsync(db);
            var page = await db.MyTrackingPages.Include(p => p.Banners).SingleAsync(p => p.CustomerId == 42, Ct);
            Assert.Equal("keep-this-url", page.Slug);
            Assert.True(page.IsPublished);
            Assert.Equal(new DateTime(2026, 10, 1), page.CreateDate);
            Assert.Equal(new DateTime(2026, 10, 2), page.ModifyDate);
            Assert.True(JsonNode.DeepEquals(config, JsonNode.Parse(page.ConfigJson)));
            Assert.Equal(2, page.Banners.Count);
            Assert.DoesNotContain("base64", page.ExtraConfigJson!);
            Assert.All(page.Banners, b => Assert.DoesNotContain("base64", b.ExtraConfigJson!));
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = 'MyTrackingCauHinh'").SingleAsync(Ct));
            Assert.Equal("#abcdef", await db.Database.SqlQuery<string>($"SELECT Color_01 AS [Value] FROM dbo.TCustomer_MyTracking WHERE CustomerID=99").SingleAsync(Ct));

            // Đọc lại phải dùng cột cũ: thay đổi từ phần mềm cũ xuất hiện ở cấu hình portal.
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.TCustomer_MyTracking SET CompanyName=N'Changed by legacy' WHERE CustomerID=42", Ct);
            db.ChangeTracker.Clear();
            var reread = await db.MyTrackingPages.Include(p => p.Banners).SingleAsync(p => p.CustomerId == 42, Ct);
            Assert.Equal("Changed by legacy", JsonNode.Parse(reread.ConfigJson)!["brand"]!["companyName"]!.GetValue<string>());

            // Script triển khai phải chạy lại được sau khi bảng nguồn đã bị xóa.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE SCHEMA [identity];
                """, Ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE [identity].[__EFMigrationsHistory] (MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL);
                INSERT [identity].[__EFMigrationsHistory] VALUES (N'20261015093129_MoveMyTrackingToLegacyTables',N'10.0.12');
                """, Ct);
            var script = db.GetService<IMigrator>().GenerateScript("20261015090242_MoveStaffToNguoiLienLac", "20261015093129_MoveMyTrackingToLegacyTables",
                MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync(Ct);
            foreach (var batch in Regex.Split(script, @"(?m)^\s*GO\s*$").Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await using var sql = db.Database.GetDbConnection().CreateCommand();
                sql.CommandText = batch;
                await sql.ExecuteNonQueryAsync(Ct);
            }
            Assert.Equal(2, await db.MyTrackingBanners.CountAsync(Ct));
        });
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Conflicting_destination_aborts_without_dropping_or_overwriting_old_data()
    {
        await WithDatabase(async db =>
        {
            var emptyConfig = "{}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.MyTrackingCauHinh (CustomerID,Duong_Dan,Cau_Hinh,Da_Xuat_Ban,CreateDate) VALUES (42,'keep-url',{emptyConfig},1,'2026-10-01');
                INSERT dbo.TCustomer_MyTracking (CustomerID,CompanyName) VALUES (42,N'Existing legacy company');
                """, Ct);
            var error = await Assert.ThrowsAsync<SqlException>(() => ApplyAsync(db));
            Assert.Equal(51000, error.Number);
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.MyTrackingCauHinh").SingleAsync(Ct));
            Assert.Equal("Existing legacy company", await db.Database.SqlQuery<string>($"SELECT CompanyName AS [Value] FROM dbo.TCustomer_MyTracking").SingleAsync(Ct));
        });
    }

    private static async Task ApplyAsync(IdentityDbContext db)
    {
        var migration = new MoveMyTrackingToLegacyTables { ActiveProvider = db.Database.ProviderName! };
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        foreach (var command in commands)
        {
            await using var sql = db.Database.GetDbConnection().CreateCommand();
            sql.Transaction = tx.GetDbTransaction();
            sql.CommandText = command.CommandText;
            await sql.ExecuteNonQueryAsync(Ct);
        }
        await tx.CommitAsync(Ct);
    }

    private static async Task WithDatabase(Func<IdentityDbContext, Task> test)
    {
        var databaseName = $"vietan_tests_mytracking_{Guid.NewGuid():N}";
        var server = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER")) { InitialCatalog = "master" };
        await using var master = new SqlConnection(server.ConnectionString);
        await master.OpenAsync(Ct);
        await using (var create = master.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync(Ct);
        }
        try
        {
            server.InitialCatalog = databaseName;
            await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlServer(server.ConnectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "legacy-tables.sql"))) directory = directory.Parent;
            var legacy = await File.ReadAllTextAsync(Path.Combine(directory!.FullName, "scripts", "legacy-tables.sql"), Ct);
            foreach (var table in new[] { "TCustomer_MyTracking", "TCustomer_MyTracking_Banner" })
            {
                var start = legacy.IndexOf($"CREATE TABLE [dbo].[{table}]", StringComparison.Ordinal);
                var end = legacy.IndexOf("\nEND", start, StringComparison.Ordinal);
                await db.Database.ExecuteSqlRawAsync(legacy[start..end], Ct);
            }
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.MyTrackingCauHinh (
                    CustomerID bigint NOT NULL PRIMARY KEY, Duong_Dan varchar(60) NOT NULL,
                    Cau_Hinh nvarchar(max) NOT NULL, Da_Xuat_Ban bit NOT NULL,
                    CreateDate datetime NOT NULL, ModifyDate datetime NULL);
                """, Ct);
            await test(db);
        }
        finally
        {
            // Chỉ database có tên GUID do chính bài kiểm thử này tạo; không xóa database ứng dụng.
            if (!databaseName.StartsWith("vietan_tests_mytracking_", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test database name");
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
