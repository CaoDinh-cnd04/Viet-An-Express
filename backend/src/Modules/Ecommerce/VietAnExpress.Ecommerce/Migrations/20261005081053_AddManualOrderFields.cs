using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Ecommerce.Shared;

#nullable disable

namespace VietAnExpress.Ecommerce.Migrations
{
    /// <inheritdoc />
    internal partial class AddManualOrderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Chi_Nhanh",
                schema: "dbo",
                table: "DonTMDT",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dich_Vu",
                schema: "dbo",
                table: "DonTMDT",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hai_Quan",
                schema: "dbo",
                table: "DonTMDT",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hub",
                schema: "dbo",
                table: "DonTMDT",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Chi_Nhanh",
                schema: "dbo",
                table: "DonTMDT");

            migrationBuilder.DropColumn(
                name: "Dich_Vu",
                schema: "dbo",
                table: "DonTMDT");

            migrationBuilder.DropColumn(
                name: "Hai_Quan",
                schema: "dbo",
                table: "DonTMDT");

            migrationBuilder.DropColumn(
                name: "Hub",
                schema: "dbo",
                table: "DonTMDT");
        }
    }
}
