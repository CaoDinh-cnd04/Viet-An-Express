using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class DropShipmentsKeepDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentPackages",
                schema: "shipments");

            migrationBuilder.DropTable(
                name: "ShipmentTrackingEvents",
                schema: "shipments");

            migrationBuilder.DropTable(
                name: "Shipments",
                schema: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_OrderDrafts_CreatedBy",
                schema: "shipments",
                table: "OrderDrafts");

            migrationBuilder.DropSequence(
                name: "ShipmentCodeSequence",
                schema: "shipments");

            // CustomerId: Guid của customers.Customers (sẽ bỏ) → dbo.TCustomer.CustomerID (bigint).
            // Nháp không đổi được sang mã khách (nhân viên tạo / khách không còn) thì bỏ.
            migrationBuilder.DropIndex(name: "IX_OrderDrafts_CustomerId", schema: "shipments", table: "OrderDrafts");
            migrationBuilder.AddColumn<long>(name: "LegacyCustomerId", schema: "shipments", table: "OrderDrafts", type: "bigint", nullable: true);
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'customers.Customers') IS NOT NULL
                    EXEC(N'UPDATE d SET LegacyCustomerId = c.LegacyId
                           FROM shipments.OrderDrafts d JOIN customers.Customers c ON c.Id = d.CustomerId');
                DELETE FROM shipments.OrderDrafts WHERE LegacyCustomerId IS NULL;
                """);
            migrationBuilder.DropColumn(name: "CustomerId", schema: "shipments", table: "OrderDrafts");
            migrationBuilder.RenameColumn(name: "LegacyCustomerId", schema: "shipments", table: "OrderDrafts", newName: "CustomerId");
            migrationBuilder.AlterColumn<long>(name: "CustomerId", schema: "shipments", table: "OrderDrafts", type: "bigint", nullable: false,
                oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
            migrationBuilder.CreateIndex(name: "IX_OrderDrafts_CustomerId", schema: "shipments", table: "OrderDrafts", column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ShipmentCodeSequence",
                schema: "shipments",
                startValue: 10000001L);

            // Không khôi phục được liên kết khách kiểu Guid — nháp cũ bị bỏ khi quay lại.
            migrationBuilder.Sql("DELETE FROM shipments.OrderDrafts;");
            migrationBuilder.DropIndex(name: "IX_OrderDrafts_CustomerId", schema: "shipments", table: "OrderDrafts");
            migrationBuilder.DropColumn(name: "CustomerId", schema: "shipments", table: "OrderDrafts");
            migrationBuilder.AddColumn<Guid>(name: "CustomerId", schema: "shipments", table: "OrderDrafts", type: "uniqueidentifier", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_OrderDrafts_CustomerId", schema: "shipments", table: "OrderDrafts", column: "CustomerId");

            migrationBuilder.CreateTable(
                name: "Shipments",
                schema: "shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualWeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    BillIssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChargeableWeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GoodsDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastFailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ServiceCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalPieces = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VolumetricWeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DeclaredValue_Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeclaredValue_Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Receiver_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Receiver_CompanyName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Receiver_ContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Receiver_CountryCode = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Receiver_Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Receiver_Line1 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Receiver_Line2 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Receiver_Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Receiver_PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Receiver_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Sender_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sender_CompanyName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Sender_ContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sender_CountryCode = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Sender_Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Sender_Line1 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Sender_Line2 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Sender_Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sender_PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Sender_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentPackages",
                schema: "shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    LengthCm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    WidthCm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentPackages_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "shipments",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentTrackingEvents",
                schema: "shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentTrackingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentTrackingEvents_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "shipments",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_CreatedBy",
                schema: "shipments",
                table: "OrderDrafts",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentPackages_ShipmentId",
                schema: "shipments",
                table: "ShipmentPackages",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_BranchId",
                schema: "shipments",
                table: "Shipments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Code",
                schema: "shipments",
                table: "Shipments",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CreatedAt",
                schema: "shipments",
                table: "Shipments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CustomerId",
                schema: "shipments",
                table: "Shipments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Status",
                schema: "shipments",
                table: "Shipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentTrackingEvents_ShipmentId_OccurredAt",
                schema: "shipments",
                table: "ShipmentTrackingEvents",
                columns: new[] { "ShipmentId", "OccurredAt" });
        }
    }
}
