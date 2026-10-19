using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <summary>
    /// Tài khoản con chuyển từ bảng tạm dbo.TaiKhoanNhanVien sang bảng người liên hệ của hệ thống cũ dbo.TCustomer_NguoiLienLac
    /// (người dùng duyệt thêm cột Quyen). Bảng cũ đã có người liên hệ nên ID mới khác ID cũ → đổi ID tài khoản con ở mọi bảng tham chiếu,
    /// rồi xóa bảng tạm.
    /// </summary>
    internal partial class MoveStaffToNguoiLienLac : Migration
    {
        /// <summary>Bảng / cột lưu ID tài khoản con (đổi theo khi chuyển bảng).</summary>
        private static readonly (string Table, string Column)[] StaffReferences =
        [
            ("SuCo", "StaffID"),
            ("VanDonNguoiTao", "StaffID"),
            ("DonTMDT", "Nhan_Vien_Tao"),
            ("ThongBao_DaDoc", "TaiKhoanCon_ID")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bảng cũ không thuộc migration: thêm tường minh cột quyền được duyệt.
            migrationBuilder.AddColumn<string>(
                name: "Quyen", schema: "dbo", table: "TCustomer_NguoiLienLac",
                type: "nvarchar(1000)", maxLength: 1000, nullable: true);

            // 1 lệnh: chép tài khoản (MERGE để lấy được cặp ID cũ → mới), đổi ID ở bảng tham chiếu.
            // Bảng tham chiếu thuộc module khác có thể chưa có → SQL động sau khi kiểm tra; SQL động đọc được bảng tạm #map cùng phiên.
            migrationBuilder.Sql($@"
CREATE TABLE #map (OldId bigint NOT NULL PRIMARY KEY, NewId bigint NOT NULL);

MERGE dbo.TCustomer_NguoiLienLac AS t
USING dbo.TaiKhoanNhanVien AS s ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (CustomerID, Ten, Di_Dong, Email, Chuc_Vu, Trang_Thai, Mo_Ta, Phong_Ban, Create_Date, TinhCach, Quyen)
    VALUES (s.CustomerID, s.Ho_Ten, s.Dien_Thoai, s.Email, 0, CASE WHEN s.Dang_Hoat_Dong = 1 THEN 1 ELSE 0 END,
            s.Mat_Khau_Bam, 0, s.CreateDate, s.Ten_Dang_Nhap, s.Quyen)
OUTPUT s.ID, inserted.ID INTO #map (OldId, NewId);
{string.Concat(Array.ConvertAll(StaffReferences, r => $@"
IF OBJECT_ID(N'dbo.{r.Table}', N'U') IS NOT NULL
    EXEC(N'UPDATE x SET {r.Column} = m.NewId FROM dbo.{r.Table} x JOIN #map m ON x.{r.Column} = m.OldId;');"))}

DROP TABLE #map;");

            migrationBuilder.DropTable(name: "TaiKhoanNhanVien", schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaiKhoanNhanVien",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Ten_Dang_Nhap = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Mat_Khau_Bam = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Ho_Ten = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Dien_Thoai = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Quyen = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    Dang_Hoat_Dong = table.Column<bool>(type: "bit", nullable: false),
                    Dang_Nhap_Lan_Cuoi = table.Column<DateTime>(type: "datetime", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_TaiKhoanNhanVien", x => x.ID));

            // Giữ nguyên ID (= ID người liên hệ) để bảng tham chiếu vẫn đúng; mật khẩu đã lưu như hệ thống cũ thì nhân viên phải được đặt lại.
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT dbo.TaiKhoanNhanVien ON;
INSERT INTO dbo.TaiKhoanNhanVien (ID, CustomerID, Ten_Dang_Nhap, Mat_Khau_Bam, Ho_Ten, Email, Dien_Thoai, Quyen, Dang_Hoat_Dong, CreateDate)
SELECT ID, CustomerID, LEFT(TinhCach, 50), ISNULL(Mo_Ta, ''), LEFT(ISNULL(Ten, ''), 100), LEFT(Email, 150), LEFT(Di_Dong, 30), ISNULL(Quyen, ''),
       CASE WHEN Trang_Thai = 1 THEN 1 ELSE 0 END, Create_Date
FROM dbo.TCustomer_NguoiLienLac
WHERE Quyen IS NOT NULL AND ISNULL(TinhCach, '') <> '';
SET IDENTITY_INSERT dbo.TaiKhoanNhanVien OFF;");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanNhanVien_CustomerID", schema: "dbo", table: "TaiKhoanNhanVien", column: "CustomerID");
            migrationBuilder.CreateIndex(
                name: "UX_TaiKhoanNhanVien_Ten_Dang_Nhap", schema: "dbo", table: "TaiKhoanNhanVien", column: "Ten_Dang_Nhap", unique: true);

            migrationBuilder.DropColumn(name: "Quyen", schema: "dbo", table: "TCustomer_NguoiLienLac");
        }
    }
}
