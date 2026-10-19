using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Shipments.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class DropOrderDraftsUseMaVanDon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDrafts",
                schema: "shipments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderDrafts",
                schema: "shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Consignee = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PiecesText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrintedOrderNumber = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDrafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_BranchId",
                schema: "shipments",
                table: "OrderDrafts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_CustomerId",
                schema: "shipments",
                table: "OrderDrafts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_PrintedOrderNumber",
                schema: "shipments",
                table: "OrderDrafts",
                column: "PrintedOrderNumber",
                filter: "[PrintedOrderNumber] IS NOT NULL");
        }
    }
}
