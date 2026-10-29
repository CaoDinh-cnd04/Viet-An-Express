using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class AddFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GopY",
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
                    Noi_Dung = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Lien_He = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Da_Xem = table.Column<bool>(type: "bit", nullable: false),
                    Ngay_Gui = table.Column<DateTime>(type: "datetime", nullable: false),
                    Ngay_Xem = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GopY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GopY_HinhAnh",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GopY_ID = table.Column<long>(type: "bigint", nullable: false),
                    Ten_File = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Loai_File = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Kich_Thuoc = table.Column<int>(type: "int", nullable: false),
                    Du_Lieu = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GopY_HinhAnh", x => x.ID);
                    table.ForeignKey(
                        name: "FK_GopY_HinhAnh_GopY_GopY_ID",
                        column: x => x.GopY_ID,
                        principalSchema: "dbo",
                        principalTable: "GopY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GopY_CustomerID_Ngay_Gui",
                schema: "dbo",
                table: "GopY",
                columns: new[] { "CustomerID", "Ngay_Gui" });

            migrationBuilder.CreateIndex(
                name: "IX_GopY_HinhAnh_GopY_ID",
                schema: "dbo",
                table: "GopY_HinhAnh",
                column: "GopY_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GopY_HinhAnh",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GopY",
                schema: "dbo");
        }
    }
}
