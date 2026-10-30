using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using VietAnExpress.Identity.Migrations;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Shared;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class MyTrackingMigrationSqlServerTests
{
    public static bool SqlServerAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Original_schema_is_restored_exactly_and_oversized_data_aborts_without_truncation()
    {
        await WithDatabase(async db =>
        {
            var original = await SchemaAsync(db);
            await ApplyAsync(db, new MoveMyTrackingToLegacyTables());
            await ApplyAsync(db, new TrimMyTrackingPortalColumns());
            await ApplyAsync(db, new RestoreMyTrackingColumnLengths());
            await ApplyAsync(db, new MoveBackgroundToStr1DropPublish());
            // DATALENGTH phải tính cả dấu cách cuối chuỗi, không chỉ LEN.
            var oversized = new string('a', 250) + " ";
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.TCustomer_MyTracking(CustomerID,CompanyName,Str1) VALUES(42,N'Legacy company',{oversized})", Ct);
            var error = await Assert.ThrowsAsync<SqlException>(() => ApplyAsync(db, new RestoreOriginalMyTrackingSchema()));
            Assert.Equal(51001, error.Number);
            Assert.Equal(oversized, await db.Database.SqlQuery<string>($"SELECT Str1 AS [Value] FROM dbo.TCustomer_MyTracking WHERE CustomerID=42").SingleAsync(Ct));
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.TCustomer_MyTracking SET Str1=N'/Upload/Banner/legacy.jpg' WHERE CustomerID=42", Ct);
            await ApplyAsync(db, new RestoreOriginalMyTrackingSchema());
            Assert.Equal(original, await SchemaAsync(db));
            Assert.Equal("Legacy company", await db.Database.SqlQuery<string>($"SELECT CompanyName AS [Value] FROM dbo.TCustomer_MyTracking WHERE CustomerID=42").SingleAsync(Ct));
            // Chạy lại script vẫn giữ nguyên cấu trúc gốc.
            await ApplyAsync(db, new RestoreOriginalMyTrackingSchema());
            Assert.Equal(original, await SchemaAsync(db));
        });
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Concurrent_saves_on_original_tables_do_not_create_duplicate_pages_without_a_unique_index()
    {
        await WithDatabase(async db =>
        {
            await using var fixture = new IdentityFixture();
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlServer(db.Database.GetConnectionString(), sql => sql.EnableRetryOnFailure()).Options;
            await using var first = new IdentityDbContext(options);
            await using var second = new IdentityDbContext(options);
            var config = JsonSerializer.SerializeToElement(MyTrackingStorageTests.FullConfigV2());
            var results = await Task.WhenAll(
                new SaveMyTrackingHandler(first, fixture.Customers.Object, fixture.CurrentUser.Object, fixture.Images, fixture.Clock).Handle(new SaveMyTrackingCommand(config), Ct),
                new SaveMyTrackingHandler(second, fixture.Customers.Object, fixture.CurrentUser.Object, fixture.Images, fixture.Clock).Handle(new SaveMyTrackingCommand(config), Ct));
            Assert.All(results, result => Assert.True(result.IsSuccess, result.Error.Message));
            Assert.Equal(1, await db.MyTrackingPages.CountAsync(Ct));
            Assert.Equal(2, await db.MyTrackingBanners.CountAsync(Ct));
        });
    }

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Brand_contacts_and_logo_are_saved_to_TCustomer_and_TCommonFile_like_the_old_system()
    {
        await WithDatabase(async db =>
        {
            await using var fixture = new IdentityFixture();
            var config = MyTrackingStorageTests.FullConfigV2();
            config["brand"]!["logo"] = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jWZkAAAAASUVORK5CYII=";
            var saved = await new SaveMyTrackingHandler(db, fixture.Customers.Object, fixture.CurrentUser.Object, fixture.Images, fixture.Clock)
                .Handle(new SaveMyTrackingCommand(JsonSerializer.SerializeToElement(config)), Ct);
            Assert.True(saved.IsSuccess, saved.Error.Message);
            var id = IdentityFixture.CustomerId;
            Assert.Equal("0901234567", await db.Database.SqlQuery<string>($"SELECT CongLienLac_Hotline AS [Value] FROM dbo.TCustomer WHERE CustomerID={id}").SingleAsync(Ct));
            Assert.Equal("https://sgb.vn", await db.Database.SqlQuery<string>($"SELECT Bank AS [Value] FROM dbo.TCustomer WHERE CustomerID={id}").SingleAsync(Ct));
            Assert.Equal("#1e6b2c", await db.Database.SqlQuery<string>($"SELECT Column1 AS [Value] FROM dbo.TCustomer WHERE CustomerID={id}").SingleAsync(Ct));
            // Địa chỉ người gửi không đổi; logo = dòng TCommonFile mới.
            Assert.Equal(IdentityFixture.BrandAddress, await db.Database.SqlQuery<string>($"SELECT Address1 AS [Value] FROM dbo.TCustomer WHERE CustomerID={id}").SingleAsync(Ct));
            var fileId = await db.Database.SqlQuery<long>($"SELECT Logo_ShippingMark AS [Value] FROM dbo.TCustomer WHERE CustomerID={id}").SingleAsync(Ct);
            Assert.Equal("/Upload/Logo_ShippingMark/", await db.Database.SqlQuery<string>($"SELECT ServerFilePath AS [Value] FROM dbo.TCommonFile WHERE CommonFileID={fileId}").SingleAsync(Ct));

            // Khách chưa khai cột thương hiệu nào vẫn đọc ra (không phải null) nhờ cột Staff_ID.
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.TCustomer SET Address1=NULL, Phone=NULL, Bank=NULL, Column1=NULL, Column2=NULL, Logo_ShippingMark=NULL, CongLienLac_Hotline=NULL, CongLienLac_WhatsApp=NULL, CongLienLac_Email=NULL", Ct);
            db.ChangeTracker.Clear();
            Assert.NotNull((await db.MyTrackingBrandAsync(id, Ct)).Brand);
        });
    }

    private static Task<string> SchemaAsync(IdentityDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT (SELECT
          JSON_QUERY((SELECT t.name AS [table],c.column_id,c.name,ty.name AS [type],c.max_length,c.is_nullable,c.is_identity,dc.definition
            FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
            WHERE t.name IN('TCustomer_MyTracking','TCustomer_MyTracking_Banner') ORDER BY t.name,c.column_id FOR JSON PATH)) AS [columns],
          JSON_QUERY((SELECT t.name AS [table],i.name,i.type,i.is_unique,i.is_primary_key,i.filter_definition,c.name AS [column],ic.key_ordinal,ic.is_descending_key
            FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE t.name IN('TCustomer_MyTracking','TCustomer_MyTracking_Banner') ORDER BY t.name,i.name,ic.key_ordinal FOR JSON PATH)) AS [indexes]
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(Ct);

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Migrations_move_old_portal_config_into_legacy_columns_then_drop_portal_only_columns()
    {
        await WithDatabase(async db =>
        {
            var config = MyTrackingStorageTests.FullConfig();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.MyTrackingCauHinh (CustomerID,Duong_Dan,Cau_Hinh,Da_Xuat_Ban,CreateDate,ModifyDate)
                VALUES (42,'keep-this-url',{config.ToJsonString()},1,'2026-10-01','2026-10-02');
                INSERT dbo.TCustomer_MyTracking (CustomerID,CompanyName,Color_01) VALUES (99,N'Existing legacy company',N'#abcdef');
                """, Ct);
            await ApplyAsync(db, new MoveMyTrackingToLegacyTables());
            await ApplyAsync(db, new TrimMyTrackingPortalColumns());
            await ApplyAsync(db, new RestoreMyTrackingColumnLengths());
            await ApplyAsync(db, new MoveBackgroundToStr1DropPublish());
            // Cột không cần độ dài lớn trở lại nvarchar(250) như bảng gốc; cột ảnh vẫn nvarchar(max).
            Assert.Equal(500, await db.Database.SqlQuery<int>(
                $"SELECT CAST(max_length AS int) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TCustomer_MyTracking') AND name = 'CongTinNhan_02'").SingleAsync(Ct));
            Assert.Equal(-1, await db.Database.SqlQuery<int>(
                $"SELECT CAST(max_length AS int) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TCustomer_MyTracking') AND name = 'Logo'").SingleAsync(Ct));
            var page = await db.MyTrackingPages.Include(p => p.Banners).SingleAsync(p => p.CustomerId == 42, Ct);
            // Ảnh nền (Hinh_Nen) sang Str1; Hinh_Nen, Da_Xuat_Ban đã xóa.
            Assert.Equal(config["background"]!.GetValue<string>(), page.Background);
            Assert.Equal(2, page.Banners.Count);
            // Zalo / WhatsApp / X portal ghi theo cách cũ đã sang ô của hệ thống cũ (_02 / _04 / CongTruyenThong_05).
            Assert.Equal(("0901234567", "+84901234567", null), (page.CongTinNhan02, page.CongTinNhan04, page.CongTinNhan01));
            Assert.Equal(("https://facebook.com/example", "https://x.com/example"), (page.CongTruyenThong01, page.CongTruyenThong05));
            foreach (var (table, column) in new[] { ("TCustomer_MyTracking", "Hinh_Nen"), ("TCustomer_MyTracking", "Da_Xuat_Ban"), ("TCustomer_MyTracking", "Duong_Dan"), ("TCustomer_MyTracking", "Cau_Hinh_Bo_Sung"),
                ("TCustomer_MyTracking", "CreateDate"), ("TCustomer_MyTracking", "ModifyDate"), ("TCustomer_MyTracking_Banner", "Cau_Hinh_Bo_Sung") })
                Assert.Equal(0, await db.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID({"dbo." + table}) AND name = {column}").SingleAsync(Ct));
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = 'MyTrackingCauHinh'").SingleAsync(Ct));
            Assert.Equal("#abcdef", await db.Database.SqlQuery<string>($"SELECT Color_01 AS [Value] FROM dbo.TCustomer_MyTracking WHERE CustomerID=99").SingleAsync(Ct));

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
            var error = await Assert.ThrowsAsync<SqlException>(() => ApplyAsync(db, new MoveMyTrackingToLegacyTables()));
            Assert.Equal(51000, error.Number);
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.MyTrackingCauHinh").SingleAsync(Ct));
            Assert.Equal("Existing legacy company", await db.Database.SqlQuery<string>($"SELECT CompanyName AS [Value] FROM dbo.TCustomer_MyTracking").SingleAsync(Ct));
        });
    }

    private static async Task ApplyAsync(IdentityDbContext db, Migration migration)
    {
        migration.ActiveProvider = db.Database.ProviderName!;
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
            // Lưu MyTracking kiểm tra quyền ở dbo.TCustomer.ViewLinkTracking (CMS bật) — bảng cũ tối thiểu, đúng các cột portal đọc.
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE TABLE dbo.TCustomer (
                    CustomerID bigint NOT NULL PRIMARY KEY, Login_UserName nvarchar(50) NULL, Login_Password nvarchar(150) NULL,
                    CustomerTypeID int NULL, ViewLinkTracking int NULL,
                    -- Thương hiệu MyTracking (MyTrackingBrand), đúng kiểu / độ dài DDL gốc.
                    Staff_ID int NOT NULL, Address1 nvarchar(250) NULL, Phone nvarchar(150) NULL, Bank nvarchar(150) NULL,
                    Logo_ShippingMark bigint NULL, Column1 nvarchar(500) NULL, Column2 nvarchar(500) NULL,
                    CongLienLac_Hotline nvarchar(100) NULL, CongLienLac_Zalo nvarchar(200) NULL, CongLienLac_Messenger nvarchar(200) NULL,
                    CongLienLac_WhatsApp nvarchar(200) NULL, CongLienLac_WeChat nvarchar(200) NULL, CongLienLac_Telegram nvarchar(200) NULL,
                    CongLienLac_Viber nvarchar(200) NULL, CongLienLac_Line nvarchar(200) NULL, CongLienLac_Snapchat nvarchar(200) NULL,
                    CongLienLac_KakaoTalk nvarchar(200) NULL, CongLienLac_Signal nvarchar(200) NULL, CongLienLac_Email nvarchar(200) NULL);
                INSERT dbo.TCustomer (CustomerID, Login_UserName, Login_Password, ViewLinkTracking, Staff_ID, Address1, Phone)
                VALUES ({IdentityFixture.CustomerId}, N'{IdentityFixture.UserName}', N'{IdentityFixture.Password}', 1, 0,
                    N'{IdentityFixture.BrandAddress}', N'{IdentityFixture.BrandPhone}');
                CREATE TABLE dbo.TCommonFile (
                    CommonFileID bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, RealFileName nvarchar(150) NOT NULL, ServerFileName nvarchar(150) NOT NULL,
                    ServerFilePath nvarchar(1000) NULL, FileSize int NOT NULL, MimeType varchar(20) NULL, RegDate datetime NOT NULL DEFAULT (getdate()));
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
