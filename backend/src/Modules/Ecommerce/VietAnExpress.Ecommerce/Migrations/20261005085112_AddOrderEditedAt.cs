using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Ecommerce.Shared;

#nullable disable

namespace VietAnExpress.Ecommerce.Migrations
{
    /// <inheritdoc />
    internal partial class AddOrderEditedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Ngay_Sua",
                schema: "dbo",
                table: "DonTMDT",
                type: "datetime",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ngay_Sua",
                schema: "dbo",
                table: "DonTMDT");
        }
    }
}
