using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <summary>
    /// Gộp thông báo về 2 bảng (người dùng chọn 27/10/2026): ThongBao → TThongBao (thêm cột JSON <c>Tin</c> từ ThongBao_Tin và
    /// <c>Khach_Nhan</c> ",id," từ ThongBao_NguoiNhan), ThongBao_DaDoc → TThongBao_DaDoc. Đổi tên bảng nên giữ nguyên ID và lượt đã đọc;
    /// dữ liệu 2 bảng phụ chép sang cột mới trước khi xóa bảng.
    /// </summary>
    internal partial class MergeNotificationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Khach_Nhan",
                schema: "dbo",
                table: "ThongBao",
                type: "varchar(max)",
                unicode: false,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tin",
                schema: "dbo",
                table: "ThongBao",
                type: "nvarchar(max)",
                nullable: true);

            // EXEC: file SQL gửi máy chủ chạy cả migration trong 1 khối lệnh — UPDATE viết thẳng sẽ lỗi biên dịch vì cột vừa thêm chưa có.
            // Các tin → mảng JSON theo thứ tự; thông báo không có tin (thông báo cũ / hỗ trợ) = "[]".
            migrationBuilder.Sql("""
                EXEC(N'UPDATE t SET Tin = ISNULL((
                    SELECT i.Thu_Tu, i.Tieu_De, i.Noi_Dung, i.Chi_Tiet, i.Hinh_Anh, i.Duong_Dan
                    FROM dbo.ThongBao_Tin i
                    WHERE i.ThongBao_ID = t.ID
                    ORDER BY i.Thu_Tu, i.ID
                    FOR JSON PATH), N''[]'')
                FROM dbo.ThongBao t;');
                """);

            // Khách chỉ định → ",201008,200877," (FOR XML PATH: chạy được cả SQL Server cũ chưa có STRING_AGG).
            migrationBuilder.Sql("""
                EXEC(N'UPDATE t SET Khach_Nhan = '','' + STUFF((
                    SELECT '','' + CAST(r.CustomerID AS varchar(20))
                    FROM dbo.ThongBao_NguoiNhan r
                    WHERE r.ThongBao_ID = t.ID
                    ORDER BY r.ID
                    FOR XML PATH('''')), 1, 1, '''') + '',''
                FROM dbo.ThongBao t
                WHERE EXISTS (SELECT 1 FROM dbo.ThongBao_NguoiNhan r WHERE r.ThongBao_ID = t.ID);');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ThongBao_DaDoc_ThongBao_ThongBao_ID",
                schema: "dbo",
                table: "ThongBao_DaDoc");

            migrationBuilder.DropTable(
                name: "ThongBao_NguoiNhan",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ThongBao_Tin",
                schema: "dbo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ThongBao_DaDoc",
                schema: "dbo",
                table: "ThongBao_DaDoc");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ThongBao",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.RenameTable(
                name: "ThongBao_DaDoc",
                schema: "dbo",
                newName: "TThongBao_DaDoc",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "ThongBao",
                schema: "dbo",
                newName: "TThongBao",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "UX_ThongBao_DaDoc_TaiKhoan",
                schema: "dbo",
                table: "TThongBao_DaDoc",
                newName: "UX_TThongBao_DaDoc_TaiKhoan");

            migrationBuilder.RenameIndex(
                name: "IX_ThongBao_DaDoc_ThongBao_ID",
                schema: "dbo",
                table: "TThongBao_DaDoc",
                newName: "IX_TThongBao_DaDoc_ThongBao_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ThongBao_Trang_Thai_Ngay_Dang",
                schema: "dbo",
                table: "TThongBao",
                newName: "IX_TThongBao_Trang_Thai_Ngay_Dang");

            migrationBuilder.RenameIndex(
                name: "IX_ThongBao_Ngay_Tao",
                schema: "dbo",
                table: "TThongBao",
                newName: "IX_TThongBao_Ngay_Tao");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TThongBao_DaDoc",
                schema: "dbo",
                table: "TThongBao_DaDoc",
                column: "ID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TThongBao",
                schema: "dbo",
                table: "TThongBao",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_TThongBao_DaDoc_TThongBao_ThongBao_ID",
                schema: "dbo",
                table: "TThongBao_DaDoc",
                column: "ThongBao_ID",
                principalSchema: "dbo",
                principalTable: "TThongBao",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Migration gộp bảng thông báo chỉ tiến tới. Khôi phục từ bản sao lưu nếu cần.");
    }
}
