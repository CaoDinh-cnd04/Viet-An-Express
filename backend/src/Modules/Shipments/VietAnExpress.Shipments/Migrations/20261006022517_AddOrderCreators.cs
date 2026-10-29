using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class AddOrderCreators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedByStaffId",
                schema: "shipments",
                table: "OrderDrafts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VanDonNguoiTao",
                schema: "dbo",
                columns: table => new
                {
                    MaVanDon_ID = table.Column<long>(type: "bigint", nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    StaffID = table.Column<long>(type: "bigint", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VanDonNguoiTao", x => x.MaVanDon_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VanDonNguoiTao_StaffID",
                schema: "dbo",
                table: "VanDonNguoiTao",
                columns: new[] { "StaffID", "MaVanDon_ID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VanDonNguoiTao",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CreatedByStaffId",
                schema: "shipments",
                table: "OrderDrafts");
        }
    }
}
