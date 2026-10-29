using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class AddCatalogMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatHangKhachHang",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Loai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Khoa = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Yeu_Thich = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Da_Xoa = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatHangKhachHang", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MatHangKhachHang_CustomerID_Loai_Khoa",
                schema: "dbo",
                table: "MatHangKhachHang",
                columns: new[] { "CustomerID", "Loai", "Khoa" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatHangKhachHang",
                schema: "dbo");
        }
    }
}
