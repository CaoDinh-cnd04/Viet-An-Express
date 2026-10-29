using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using Xunit;

namespace VietAnExpress.Ecommerce.Tests;

/// <summary>
/// Model EF trong code phải khớp snapshot migration mới nhất — đổi entity / cấu hình mà quên tạo migration, hoặc đổi tên kiểu /
/// namespace làm snapshot lệch, thì test này báo ngay (không cần SQL Server: chỉ dựng model, không mở kết nối).
/// </summary>
public class MigrationSnapshotTests
{
    [Fact]
    public void Model_khop_snapshot_migration()
    {
        using var db = new EcommerceDbContext(new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseSqlServer("Server=model-check;Database=model-check", sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", EcommerceDbContext.Schema))
            .Options);

        Assert.False(db.Database.HasPendingModelChanges(), "Model EF lệch snapshot — chạy backend/scripts/add-migration.ps1 hoặc sửa lại thay đổi vừa làm.");
    }
}
