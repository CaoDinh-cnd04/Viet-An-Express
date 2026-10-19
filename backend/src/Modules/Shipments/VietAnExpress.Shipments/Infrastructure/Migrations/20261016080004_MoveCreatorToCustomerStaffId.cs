using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Shipments.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class MoveCreatorToCustomerStaffId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Người tạo vận đơn chuyển sang cột có sẵn của hệ thống cũ MaVanDon.Customer_Staff_ID
            // (người liên hệ / nhân viên của khách = dbo.TCustomer_NguoiLienLac.ID) rồi bỏ bảng phụ.
            migrationBuilder.Sql(@"
UPDATE m SET m.Customer_Staff_ID = c.StaffID
FROM dbo.MaVanDon m
JOIN dbo.VanDonNguoiTao c ON c.MaVanDon_ID = m.ID;");

            migrationBuilder.DropTable(
                name: "VanDonNguoiTao",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VanDonNguoiTao",
                schema: "dbo",
                columns: table => new
                {
                    MaVanDon_ID = table.Column<long>(type: "bigint", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    StaffID = table.Column<long>(type: "bigint", nullable: false)
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

            migrationBuilder.Sql(@"
INSERT dbo.VanDonNguoiTao (MaVanDon_ID, CustomerID, StaffID)
SELECT ID, ISNULL(CustomerID, 0), Customer_Staff_ID
FROM dbo.MaVanDon
WHERE Customer_Staff_ID > 0;");
        }
    }
}
