using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace VietAnExpress.SharedKernel.Persistence;

/// <summary>
/// Ghi đồng thời trên cùng database với hệ thống cũ / CMS: khóa ứng dụng của SQL Server và nhận biết lỗi trùng khóa duy nhất.
/// </summary>
public static class DbConcurrency
{
    /// <summary>Chờ lấy khóa tối đa 15 giây rồi báo lỗi (tránh treo request).</summary>
    private const int LockTimeoutMs = 15_000;

    /// <summary>Lỗi SQL Server 2601 / 2627: ghi trùng index / khóa duy nhất (2 người ghi cùng lúc).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };

    /// <summary>
    /// Chạy <paramref name="work"/> trong transaction giữ khóa ứng dụng <paramref name="resource"/> (sp_getapplock):
    /// 2 request portal cùng khóa phải chờ nhau — dùng cho kiểm tra rồi mới ghi (vd tên đăng nhập chưa ai dùng).
    /// Mỗi lần chạy (kể cả khi tự chạy lại vì lỗi kết nối) bắt đầu với change tracker trống, nên <paramref name="work"/>
    /// phải tự đọc mọi thứ cần ghi. Provider không phải SQL (test InMemory) thì chạy thẳng.
    /// </summary>
    public static async Task<T> WithAppLockAsync<T>(this DbContext db, string resource, Func<Task<T>> work, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return await work();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlAsync($"""
                DECLARE @r int;
                EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {LockTimeoutMs};
                IF @r < 0 THROW 50001, N'Hệ thống đang bận, vui lòng thử lại', 1;
                """, ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}
