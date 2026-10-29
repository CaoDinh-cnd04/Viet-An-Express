using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Migrations;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>Gộp 4 bảng thông báo về TThongBao + TThongBao_DaDoc trên SQL Server thật (database tạm, tên GUID).</summary>
public class NotificationMergeSqlServerTests
{
    public static bool SqlServerAvailable => MyTrackingMigrationSqlServerTests.SqlServerAvailable;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Gop_bang_giu_tin_khach_nhan_luot_doc_va_EF_doc_ghi_duoc_cot_JSON()
    {
        var databaseName = $"vietan_tests_notify_{Guid.NewGuid():N}";
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
            await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(server.ConnectionString).Options);
            // 4 bảng như sau migration NotificationItemDetail (tên ràng buộc / index đúng bản cũ).
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.ThongBao (
                    ID bigint IDENTITY NOT NULL CONSTRAINT PK_ThongBao PRIMARY KEY, Tieu_De nvarchar(200) NOT NULL, Noi_Dung nvarchar(4000) NOT NULL,
                    Quan_Trong bit NOT NULL, Ngay_Tao datetime NOT NULL, Nguoi_Tao nvarchar(100) NOT NULL, Email_Trang_Thai varchar(20) NOT NULL,
                    Email_So_Nguoi_Nhan int NOT NULL, Email_Da_Gui int NOT NULL, Email_Loi nvarchar(500) NULL, Duong_Dan nvarchar(500) NULL,
                    CustomerID bigint NULL, Doi_Tuong varchar(10) NOT NULL, Ghim bit NOT NULL, Loai_Khach varchar(200) NULL,
                    Ngay_Dang datetime NOT NULL, Ngay_Het_Han datetime NULL, Trang_Thai int NOT NULL);
                CREATE INDEX IX_ThongBao_Ngay_Tao ON dbo.ThongBao (Ngay_Tao);
                CREATE INDEX IX_ThongBao_Trang_Thai_Ngay_Dang ON dbo.ThongBao (Trang_Thai, Ngay_Dang);
                CREATE TABLE dbo.ThongBao_Tin (
                    ID bigint IDENTITY NOT NULL CONSTRAINT PK_ThongBao_Tin PRIMARY KEY,
                    ThongBao_ID bigint NOT NULL CONSTRAINT FK_ThongBao_Tin_ThongBao_ThongBao_ID REFERENCES dbo.ThongBao (ID) ON DELETE CASCADE,
                    Thu_Tu int NOT NULL, Tieu_De nvarchar(200) NULL, Noi_Dung nvarchar(max) NOT NULL, Chi_Tiet nvarchar(max) NULL,
                    Hinh_Anh nvarchar(2000) NULL, Duong_Dan nvarchar(500) NULL);
                CREATE TABLE dbo.ThongBao_NguoiNhan (
                    ID bigint IDENTITY NOT NULL CONSTRAINT PK_ThongBao_NguoiNhan PRIMARY KEY,
                    ThongBao_ID bigint NOT NULL CONSTRAINT FK_ThongBao_NguoiNhan_ThongBao_ThongBao_ID REFERENCES dbo.ThongBao (ID) ON DELETE CASCADE,
                    CustomerID bigint NOT NULL);
                CREATE TABLE dbo.ThongBao_DaDoc (
                    ID bigint IDENTITY NOT NULL CONSTRAINT PK_ThongBao_DaDoc PRIMARY KEY,
                    ThongBao_ID bigint NOT NULL CONSTRAINT FK_ThongBao_DaDoc_ThongBao_ThongBao_ID REFERENCES dbo.ThongBao (ID) ON DELETE CASCADE,
                    CustomerID bigint NOT NULL, TaiKhoanCon_ID bigint NOT NULL, Ngay_Doc datetime NOT NULL);
                CREATE UNIQUE INDEX UX_ThongBao_DaDoc_TaiKhoan ON dbo.ThongBao_DaDoc (CustomerID, TaiKhoanCon_ID, ThongBao_ID);
                CREATE INDEX IX_ThongBao_DaDoc_ThongBao_ID ON dbo.ThongBao_DaDoc (ThongBao_ID);

                INSERT dbo.ThongBao (Tieu_De, Noi_Dung, Quan_Trong, Ngay_Tao, Nguoi_Tao, Email_Trang_Thai, Email_So_Nguoi_Nhan, Email_Da_Gui,
                    Doi_Tuong, Ghim, Ngay_Dang, Trang_Thai)
                VALUES (N'Phụ phí', N'tóm tắt', 0, '2026-10-20', N'admin', 'none', 0, 0, 'pick', 1, '2026-10-20', 1),
                       (N'Hỗ trợ', N'CS trả lời', 0, '2026-10-21', N'cs', 'none', 0, 0, 'all', 0, '2026-10-21', 1);
                INSERT dbo.ThongBao_Tin (ThongBao_ID, Thu_Tu, Tieu_De, Noi_Dung, Hinh_Anh, Duong_Dan)
                VALUES (1, 1, N'Tin 2', N'<b>Áp dụng</b> 1/11', NULL, NULL), (1, 0, N'Tin 1', N'Tăng 3%', N'/Upload/ThongBao/a.png', N'https://vietan.vn/x');
                INSERT dbo.ThongBao_NguoiNhan (ThongBao_ID, CustomerID) VALUES (1, 201008), (1, 200877);
                INSERT dbo.ThongBao_DaDoc (ThongBao_ID, CustomerID, TaiKhoanCon_ID, Ngay_Doc) VALUES (1, 201008, 0, '2026-10-22');
                """, Ct);

            var migration = new MergeNotificationTables { ActiveProvider = db.Database.ProviderName! };
            foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText, Ct);

            Assert.Equal(["TThongBao", "TThongBao_DaDoc"], await db.Database.SqlQuery<string>(
                $"SELECT name AS [Value] FROM sys.tables WHERE name LIKE '%ThongBao%' ORDER BY name").ToListAsync(Ct));

            var notice = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == 1, Ct);
            Assert.Equal(["Tin 1", "Tin 2"], notice.Items.OrderBy(i => i.SortOrder).Select(i => i.Title));
            Assert.Equal(["/Upload/ThongBao/a.png"], notice.Items.Single(i => i.SortOrder == 0).ImagePaths);
            Assert.Equal("<b>Áp dụng</b> 1/11", notice.Items.Single(i => i.SortOrder == 1).Body);
            Assert.Equal([201008L, 200877L], notice.RecipientIds);
            Assert.Empty((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == 2, Ct)).Items);
            Assert.Equal(1, await db.NotificationReads.CountAsync(r => r.NotificationId == 1, Ct));

            // Khách chỉ định thấy, khách khác (kể cả mã chứa chuỗi con 20100) không thấy.
            var now = new DateTime(2026, 10, 27);
            Assert.Equal(2, await db.Notifications.VisibleTo(db, 201008, null, now).CountAsync(Ct));
            Assert.Equal(1, await db.Notifications.VisibleTo(db, 20100, null, now).CountAsync(Ct));

            // EF ghi lại cột JSON khi sửa nội dung.
            var tracked = await db.Notifications.SingleAsync(n => n.Id == 1, Ct);
            tracked.SetContent("Phụ phí (sửa)", [new NotificationItem(null, "Chỉ 1 tin", [], null)], false, false, null,
                NotificationAudience.Customers, [], [200877]);
            await db.SaveChangesAsync(Ct);
            db.ChangeTracker.Clear();
            var saved = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == 1, Ct);
            Assert.Equal(("Chỉ 1 tin", ",200877,"), (Assert.Single(saved.Items).Body, saved.Recipients));
        }
        finally
        {
            if (!databaseName.StartsWith("vietan_tests_notify_", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test database name");
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
