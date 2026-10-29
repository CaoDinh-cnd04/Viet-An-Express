using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class AddTroubles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerID",
                schema: "dbo",
                table: "ThongBao",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SuCo",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Ma_Khach = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ten_Cong_Ty = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    StaffID = table.Column<long>(type: "bigint", nullable: true),
                    Nguoi_Gui = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ma_Van_Don = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Nguoi_Nhan = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Quoc_Gia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Loai_Su_Co = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Muc_Do = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Mo_Ta = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Nguoi_Yeu_Cau = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Lien_He = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Trang_Thai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Ngay_Tao = table.Column<DateTime>(type: "datetime", nullable: false),
                    Ngay_Cap_Nhat = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuCo", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SuCo_TraoDoi",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SuCo_ID = table.Column<long>(type: "bigint", nullable: false),
                    Tu_Viet_An = table.Column<bool>(type: "bit", nullable: false),
                    Nguoi_Viet = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Noi_Dung = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Trang_Thai_Moi = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Ngay_Tao = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuCo_TraoDoi", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SuCo_TraoDoi_SuCo_SuCo_ID",
                        column: x => x.SuCo_ID,
                        principalSchema: "dbo",
                        principalTable: "SuCo",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuCo_CustomerID_Ngay_Cap_Nhat",
                schema: "dbo",
                table: "SuCo",
                columns: new[] { "CustomerID", "Ngay_Cap_Nhat" });

            migrationBuilder.CreateIndex(
                name: "IX_SuCo_TraoDoi_SuCo_ID",
                schema: "dbo",
                table: "SuCo_TraoDoi",
                column: "SuCo_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuCo_TraoDoi",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SuCo",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CustomerID",
                schema: "dbo",
                table: "ThongBao");
        }
    }
}
