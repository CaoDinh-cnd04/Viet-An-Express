using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class AddStaffAccountsAndMyTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "MyTrackingCauHinh",
                schema: "dbo",
                columns: table => new
                {
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Duong_Dan = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Cau_Hinh = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Da_Xuat_Ban = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyTrackingCauHinh", x => x.CustomerID);
                });

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
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoanNhanVien", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MyTrackingCauHinh_Duong_Dan",
                schema: "dbo",
                table: "MyTrackingCauHinh",
                column: "Duong_Dan",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanNhanVien_CustomerID",
                schema: "dbo",
                table: "TaiKhoanNhanVien",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "UX_TaiKhoanNhanVien_Ten_Dang_Nhap",
                schema: "dbo",
                table: "TaiKhoanNhanVien",
                column: "Ten_Dang_Nhap",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MyTrackingCauHinh",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TaiKhoanNhanVien",
                schema: "dbo");
        }
    }
}
