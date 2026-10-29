using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <summary>
    /// MyTracking ghi thẳng vào cột có sẵn của bảng cũ (người dùng yêu cầu 23/10/2026): bỏ các cột portal đã thêm nay thừa —
    /// Duong_Dan (link /t/{slug}, nay link theo mã khách), Cau_Hinh_Bo_Sung (2 bảng), CreateDate, ModifyDate.
    /// Giữ Da_Xuat_Ban, Hinh_Nen. Bản ghi portal lưu trước khi ghi đúng ô được chuyển sang ô mới trước khi xóa JSON.
    /// </summary>
    internal partial class TrimMyTrackingPortalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Portal lưu trước 23/10/2026 (có JSON, chưa có "columnMap":2) ghi CongTinNhan_01 Zalo, _02 WhatsApp, CongTruyenThong_03 X
            //    → chuyển sang ô của hệ thống cũ: Zalo _02, WhatsApp _04, Twitter (X) CongTruyenThong_05. Bản ghi phần mềm cũ (không JSON) giữ nguyên.
            //    Lệnh UPDATE dùng giá trị cũ của mọi cột ở vế phải nên đổi chỗ cùng lúc an toàn.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'Cau_Hinh_Bo_Sung') IS NOT NULL
                    EXEC(N'UPDATE dbo.TCustomer_MyTracking
                        SET CongTinNhan_01 = NULL, CongTinNhan_02 = CongTinNhan_01, CongTinNhan_04 = CongTinNhan_02,
                            CongTruyenThong_03 = NULL, CongTruyenThong_05 = CongTruyenThong_03
                        WHERE Cau_Hinh_Bo_Sung IS NOT NULL AND Cau_Hinh_Bo_Sung NOT LIKE N''%"columnMap":2%''');
                """);

            // 2. Index đường dẫn /t/{slug}.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TCustomer_MyTracking_Duong_Dan' AND object_id = OBJECT_ID(N'dbo.TCustomer_MyTracking'))
                    DROP INDEX [UX_TCustomer_MyTracking_Duong_Dan] ON [dbo].[TCustomer_MyTracking];
                """);

            // 3. Xóa cột (kèm ràng buộc mặc định nếu có, vd CreateDate DEFAULT GETDATE()). Cột không còn thì bỏ qua.
            foreach (var (table, column) in new[]
            {
                ("TCustomer_MyTracking", "Duong_Dan"),
                ("TCustomer_MyTracking", "Cau_Hinh_Bo_Sung"),
                ("TCustomer_MyTracking", "CreateDate"),
                ("TCustomer_MyTracking", "ModifyDate"),
                ("TCustomer_MyTracking_Banner", "Cau_Hinh_Bo_Sung")
            })
            {
                migrationBuilder.Sql($"""
                    IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NOT NULL
                    BEGIN
                        DECLARE @df sysname = (SELECT d.name FROM sys.default_constraints d
                            JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
                            WHERE d.parent_object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'{column}');
                        IF @df IS NOT NULL EXEC(N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df + N']');
                        ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            // Không tự dựng lại: dữ liệu JSON / đường dẫn đã xóa không khôi phục được — dùng bản sao lưu nếu cần.
            throw new NotSupportedException("Không hoàn tác được việc xóa cột MyTracking; khôi phục từ bản sao lưu database.");
    }
}
