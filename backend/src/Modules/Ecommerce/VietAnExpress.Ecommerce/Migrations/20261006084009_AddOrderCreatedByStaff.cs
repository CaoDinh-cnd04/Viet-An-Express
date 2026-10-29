using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Ecommerce.Shared;

#nullable disable

namespace VietAnExpress.Ecommerce.Migrations
{
    /// <inheritdoc />
    internal partial class AddOrderCreatedByStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Nhan_Vien_Tao",
                schema: "dbo",
                table: "DonTMDT",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonTMDT_CustomerID_Nhan_Vien_Tao",
                schema: "dbo",
                table: "DonTMDT",
                columns: new[] { "CustomerID", "Nhan_Vien_Tao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DonTMDT_CustomerID_Nhan_Vien_Tao",
                schema: "dbo",
                table: "DonTMDT");

            migrationBuilder.DropColumn(
                name: "Nhan_Vien_Tao",
                schema: "dbo",
                table: "DonTMDT");
        }
    }
}
