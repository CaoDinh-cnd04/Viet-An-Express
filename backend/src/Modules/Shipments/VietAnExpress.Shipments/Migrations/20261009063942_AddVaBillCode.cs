using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class AddVaBillCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MaVanDon được ExcludeFromMigrations: bổ sung tường minh cột mã VA bill hiển thị (VAHCM6003585US), NULL ở đơn cũ.
            migrationBuilder.AddColumn<string>(
                name: "VA_Bill", schema: "dbo", table: "MaVanDon",
                type: "nvarchar(30)", maxLength: 30, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VA_Bill", schema: "dbo", table: "MaVanDon");
        }
    }
}
