using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class AddSenderInfoRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YeuCauSuaNguoiGui",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Ma_Khach = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Nguoi_Gui = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ten_Cong_Ty_Cu = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Nguoi_Lien_He_Cu = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Dien_Thoai_Cu = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email_Cu = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Dia_Chi_Cu = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ma_So_Thue_Cu = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ten_Cong_Ty_Moi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Nguoi_Lien_He_Moi = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Dien_Thoai_Moi = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email_Moi = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Dia_Chi_Moi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ma_So_Thue_Moi = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ghi_Chu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Trang_Thai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Ly_Do_Tu_Choi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Nguoi_Duyet = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ngay_Tao = table.Column<DateTime>(type: "datetime", nullable: false),
                    Ngay_Duyet = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauSuaNguoiGui", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauSuaNguoiGui_CustomerID_Ngay_Tao",
                schema: "dbo",
                table: "YeuCauSuaNguoiGui",
                columns: new[] { "CustomerID", "Ngay_Tao" });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauSuaNguoiGui_Trang_Thai_Ngay_Tao",
                schema: "dbo",
                table: "YeuCauSuaNguoiGui",
                columns: new[] { "Trang_Thai", "Ngay_Tao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YeuCauSuaNguoiGui",
                schema: "dbo");
        }
    }
}
