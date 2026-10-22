using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Shipments.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddInvoiceLinePackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MaVanDon_ChiTietHang được ExcludeFromMigrations: bổ sung tường minh cột người dùng đã duyệt (20/10/2026).
            // Kien = số thứ tự kiện của dòng hàng khi tuyến khai invoice riêng từng kiện; NULL = invoice chung.
            migrationBuilder.AddColumn<int>(
                name: "Kien", schema: "dbo", table: "MaVanDon_ChiTietHang",
                type: "int", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Kien", schema: "dbo", table: "MaVanDon_ChiTietHang");
        }
    }
}
