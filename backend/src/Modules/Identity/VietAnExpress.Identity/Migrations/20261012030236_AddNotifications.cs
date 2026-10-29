using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThongBao",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tieu_De = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Noi_Dung = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Quan_Trong = table.Column<bool>(type: "bit", nullable: false),
                    Ngay_Tao = table.Column<DateTime>(type: "datetime", nullable: false),
                    Nguoi_Tao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email_Trang_Thai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Email_So_Nguoi_Nhan = table.Column<int>(type: "int", nullable: false),
                    Email_Da_Gui = table.Column<int>(type: "int", nullable: false),
                    Email_Loi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBao", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ThongBao_DaDoc",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThongBao_ID = table.Column<long>(type: "bigint", nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    TaiKhoanCon_ID = table.Column<long>(type: "bigint", nullable: false),
                    Ngay_Doc = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBao_DaDoc", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ThongBao_DaDoc_ThongBao_ThongBao_ID",
                        column: x => x.ThongBao_ID,
                        principalSchema: "dbo",
                        principalTable: "ThongBao",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_Ngay_Tao",
                schema: "dbo",
                table: "ThongBao",
                column: "Ngay_Tao");

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_DaDoc_ThongBao_ID",
                schema: "dbo",
                table: "ThongBao_DaDoc",
                column: "ThongBao_ID");

            migrationBuilder.CreateIndex(
                name: "UX_ThongBao_DaDoc_TaiKhoan",
                schema: "dbo",
                table: "ThongBao_DaDoc",
                columns: new[] { "CustomerID", "TaiKhoanCon_ID", "ThongBao_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThongBao_DaDoc",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ThongBao",
                schema: "dbo");
        }
    }
}
