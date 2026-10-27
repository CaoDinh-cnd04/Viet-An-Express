using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.Shipments.Contracts;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>
/// Hỗ trợ &amp; sự cố + thông báo ghi / đọc đúng bảng trên SQL Server thật (database tạm, tên GUID): bảng cũ TContact tạo bằng đúng DDL trong
/// backend/scripts/legacy-tables.sql, kiểm tra lại bằng SQL thô từng cột.
/// </summary>
public class TroubleTablesSqlServerTests
{
    public static bool SqlServerAvailable => MyTrackingMigrationSqlServerTests.SqlServerAvailable;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Bill = "6010851";

    [Fact(Skip = "Set VIETAN_TEST_SQLSERVER to test on a disposable SQL Server database", SkipUnless = nameof(SqlServerAvailable))]
    public async Task Ho_tro_su_co_va_gop_y_vao_TContact_tra_loi_vao_TThongBao()
    {
        var name = $"vietan_tests_trouble_{Guid.NewGuid():N}";
        var server = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VIETAN_TEST_SQLSERVER")) { InitialCatalog = "master" };
        await using var master = new SqlConnection(server.ConnectionString);
        await master.OpenAsync(Ct);
        await Exec(master, $"CREATE DATABASE [{name}]");
        try
        {
            server.InitialCatalog = name;
            await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(server.ConnectionString).Options);
            await db.Database.EnsureCreatedAsync(Ct); // bảng portal (TThongBao…); bảng cũ không thuộc migration → tạo bằng DDL gốc
            await db.Database.ExecuteSqlRawAsync(LegacyDdl("legacy-tables.sql", "TContact"), Ct);

            var f = new IdentityFixture();
            await using var _ = f;
            var shipments = new Mock<IShipmentsApi>();
            shipments.Setup(s => s.FindBillAsync(IdentityFixture.CustomerId, Bill, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BillSummary(Bill, "Daniel Nowotnik", "Norway"));
            f.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new CustomerSummary(IdentityFixture.CustomerId, "SaigonbayHN", "SGB EXPRESS HN", null, null, null, null, null)]);
            var customer = new CustomerTroubleHandlers(db, f.CurrentUser.Object, f.Customers.Object, shipments.Object, f.Jobs, f.Company, f.Clock);
            var admin = new AdminTroubleHandlers(db, f.CurrentUser.Object, f.Customers.Object, f.Company, f.Clock);

            // 1. Hỗ trợ & sự cố có mã vận đơn → TContact "Portal - Hỗ trợ", Des01 = bill, KeyWord = vấn đề, CustID = khách.
            var withBill = await Ok(customer.Handle(new CreateTroubleCommand(
                new(null, Bill, null, null, "Giao chậm / trễ hẹn", null, "high", "Hàng đi Na Uy 7 ngày chưa tới.", "Lan", "0909123456")), Ct));
            Assert.StartsWith("TRB-", withBill.Id);
            Assert.Equal(("Portal - Hỗ trợ", Bill, "Giao chậm / trễ hẹn", "high", 201008L, 0), await Row<(string, string, string, string, long, int)>(db,
                $"SELECT FormType, Des01, KeyWord, Des02, CustID, Status FROM dbo.TContact WHERE ID = {withBill.Id[4..]}",
                r => (r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetInt32(5))));
            // 2. Hỗ trợ không gắn vận đơn + góp ý → TContact; không đụng bảng nào khác.
            await Ok(customer.Handle(new CreateTroubleCommand(
                new(TroubleKind.Support, null, null, null, "Cước & phụ phí", "Xin bảng giá Canada", null, "Gửi giúp bảng giá tháng 11.", null, "0909123456")), Ct));
            await Ok(customer.Handle(new CreateTroubleCommand(
                new(TroubleKind.Feedback, null, null, null, null, null, null, "Nên có nút sao chép mã bill.", null, null)), Ct));
            // Góp ý ghi giống form "Yêu cầu" Bill Online: KeyWord "Feedback", Title = loại, Des02 NULL, Gender 1, FileID1-3 = 0.
            Assert.Equal(("Feedback", "Khác", true, 1, 0L), await Row<(string, string, bool, int, long)>(db,
                "SELECT KeyWord, Title, CAST(CASE WHEN Des02 IS NULL THEN 1 ELSE 0 END AS bit), Gender, FileID1 + FileID2 + FileID3 FROM dbo.TContact WHERE FormType = N'Portal - Góp ý'",
                r => (r.GetString(0), r.GetString(1), r.GetBoolean(2), r.GetInt32(3), r.GetInt64(4))));
            Assert.Equal("Portal - Góp ý|Portal - Hỗ trợ|Portal - Hỗ trợ:" + Bill, await Row(db,
                "SELECT STRING_AGG(FormType + CASE WHEN Des01 IS NULL THEN '' ELSE ':' + Des01 END, '|') WITHIN GROUP (ORDER BY FormType + ISNULL(':' + Des01, '')) FROM dbo.TContact WHERE CustID = 201008",
                r => r.GetString(0)));

            // 3. Quản trị trả lời → TContact.Status 1 + thông báo riêng ở TThongBao.
            await Ok(admin.Handle(new AdminReplyTroubleCommand(withBill.Id, "Đã liên hệ hãng, dự kiến giao 29/10.", TroubleStatus.Doing), Ct));
            Assert.Equal(1, await Scalar(db, $"SELECT Status FROM dbo.TContact WHERE ID = {withBill.Id[4..]}"));
            Assert.Equal((201008L, "[]"), await Row<(long, string)>(db,
                "SELECT CustomerID, Tin FROM dbo.TThongBao WHERE Noi_Dung LIKE N'%dự kiến giao 29/10%'", r => (r.GetInt64(0), r.GetString(1))));

            // 4. Đọc lại: khách thấy 3 yêu cầu, trang quản trị lọc được theo nhân viên.
            db.ChangeTracker.Clear();
            Assert.Equal(3, (await Ok(customer.Handle(new GetTroublesQuery(), Ct))).Count);
            Assert.Equal(3, (await admin.Handle(new GetAdminTroublesQuery(), Ct)).Count);
            f.Customers.Setup(c => c.GetCustomerIdsOfManagerAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync([IdentityFixture.CustomerId]);
            f.Customers.Setup(c => c.GetCustomerIdsOfManagerAsync(16, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            Assert.Equal(3, (await admin.Handle(new GetAdminTroublesQuery(15), Ct)).Count);
            Assert.Empty(await admin.Handle(new GetAdminTroublesQuery(16), Ct));
        }
        finally
        {
            if (!name.StartsWith("vietan_tests_trouble_", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test database name");
            await Exec(master, $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");
        }
    }

    private static async Task<T> Ok<T>(Task<VietAnExpress.SharedKernel.Results.Result<T>> task)
    {
        var r = await task;
        Assert.True(r.IsSuccess, r.Error.Message);
        return r.Value;
    }

    /// <summary>CREATE TABLE gốc của 1 bảng trong file script backend/scripts (khối IF OBJECT_ID … BEGIN … END).</summary>
    private static string LegacyDdl(string file, string table)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scripts", file))) dir = dir.Parent;
        var sql = File.ReadAllText(Path.Combine(dir!.FullName, "scripts", file));
        var start = sql.IndexOf($"CREATE TABLE [dbo].[{table}](", StringComparison.Ordinal);
        return sql[start..sql.IndexOf("\nEND", start, StringComparison.Ordinal)];
    }

    private static async Task Exec(SqlConnection c, string sql)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task<int> Scalar(IdentityDbContext db, string sql) => await Row(db, sql, r => Convert.ToInt32(r.GetValue(0)));

    private static async Task<T> Row<T>(IdentityDbContext db, string sql, Func<System.Data.Common.DbDataReader, T> read)
    {
        var c = db.Database.GetDbConnection();
        if (c.State != System.Data.ConnectionState.Open) await c.OpenAsync(Ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync(Ct);
        Assert.True(await r.ReadAsync(Ct), sql);
        return read(r);
    }
}
