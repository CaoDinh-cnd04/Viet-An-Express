using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <summary>
    /// Bỏ 2 cột portal còn thêm vào dbo.TCustomer_MyTracking (người dùng yêu cầu 23/10/2026):
    /// - Ảnh nền: hệ thống cũ lưu ở cột Str1 ("/Upload/Banner/…") → chuyển Hinh_Nen sang Str1 (chỉ dòng Str1 còn trống),
    ///   nới Str1 lên nvarchar(max) để giữ ảnh tải lên dạng data URL (như Logo), rồi xóa Hinh_Nen.
    /// - Da_Xuat_Ban: bảng cũ không có trạng thái xuất bản — xóa; khách có dòng cấu hình là trang hiện công khai.
    /// </summary>
    internal partial class MoveBackgroundToStr1DropPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Str1] nvarchar(max) NULL;");
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'Hinh_Nen') IS NOT NULL
                    EXEC(N'UPDATE dbo.TCustomer_MyTracking SET Str1 = Hinh_Nen
                        WHERE Hinh_Nen IS NOT NULL AND Hinh_Nen <> N'''' AND (Str1 IS NULL OR Str1 = N'''')');
                """);
            foreach (var column in new[] { "Hinh_Nen", "Da_Xuat_Ban" })
            {
                migrationBuilder.Sql($"""
                    IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'{column}') IS NOT NULL
                    BEGIN
                        DECLARE @df sysname = (SELECT d.name FROM sys.default_constraints d
                            JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
                            WHERE d.parent_object_id = OBJECT_ID(N'dbo.TCustomer_MyTracking') AND c.name = N'{column}');
                        IF @df IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT [' + @df + N']');
                        ALTER TABLE [dbo].[TCustomer_MyTracking] DROP COLUMN [{column}];
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Không hoàn tác được việc xóa cột MyTracking; khôi phục từ bản sao lưu database.");
    }
}
