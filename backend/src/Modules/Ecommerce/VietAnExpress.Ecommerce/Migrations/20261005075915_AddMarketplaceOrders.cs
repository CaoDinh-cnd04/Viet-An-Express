using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Ecommerce.Shared;

#nullable disable

namespace VietAnExpress.Ecommerce.Migrations
{
    /// <inheritdoc />
    internal partial class AddMarketplaceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonTMDT",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    KetNoiTMDT_ID = table.Column<long>(type: "bigint", nullable: true),
                    Nguon = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Ma_Don_San = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    So_Don = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Bill = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Trang_Thai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Nguoi_Nhan = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cong_Ty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Dien_Thoai = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Dia_Chi1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Dia_Chi2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Thanh_Pho = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Bang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ma_Buu_Chinh = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Ma_Nuoc = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: true),
                    Ten_Nuoc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    So_San_Pham = table.Column<int>(type: "int", nullable: false),
                    Can_Nang = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    Tien_Te = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: true),
                    Tong_Tien = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    San_Pham = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ghi_Chu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ngay_Dat_San = table.Column<DateTime>(type: "datetime", nullable: true),
                    Ngay_Day_Tracking = table.Column<DateTime>(type: "datetime", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonTMDT", x => x.ID);
                    table.CheckConstraint("CK_DonTMDT_Trang_Thai", "[Trang_Thai] IN ('created', 'picked_up', 'departed', 'delivered', 'exception', 'weighing')");
                    table.ForeignKey(
                        name: "FK_DonTMDT_KetNoiTMDT_ID",
                        column: x => x.KetNoiTMDT_ID,
                        principalSchema: "dbo",
                        principalTable: "KetNoiTMDT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonTMDT_CustomerID_Ngay_Dat_San",
                schema: "dbo",
                table: "DonTMDT",
                columns: new[] { "CustomerID", "Ngay_Dat_San" });

            migrationBuilder.CreateIndex(
                name: "IX_DonTMDT_KetNoiTMDT_ID",
                schema: "dbo",
                table: "DonTMDT",
                column: "KetNoiTMDT_ID");

            migrationBuilder.CreateIndex(
                name: "UX_DonTMDT_CustomerID_Nguon_Ma_Don_San",
                schema: "dbo",
                table: "DonTMDT",
                columns: new[] { "CustomerID", "Nguon", "Ma_Don_San" },
                unique: true,
                filter: "[Ma_Don_San] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonTMDT",
                schema: "dbo");
        }
    }
}
