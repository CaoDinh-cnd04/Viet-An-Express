using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class AddDraftPrintedOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PrintedOrderNumber",
                schema: "shipments",
                table: "OrderDrafts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_PrintedOrderNumber",
                schema: "shipments",
                table: "OrderDrafts",
                column: "PrintedOrderNumber",
                filter: "[PrintedOrderNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderDrafts_PrintedOrderNumber",
                schema: "shipments",
                table: "OrderDrafts");

            migrationBuilder.DropColumn(
                name: "PrintedOrderNumber",
                schema: "shipments",
                table: "OrderDrafts");
        }
    }
}
