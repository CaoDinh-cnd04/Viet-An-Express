using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class NotificationItemDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Noi_Dung",
                schema: "dbo",
                table: "ThongBao_Tin",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AddColumn<string>(
                name: "Chi_Tiet",
                schema: "dbo",
                table: "ThongBao_Tin",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Chi_Tiet",
                schema: "dbo",
                table: "ThongBao_Tin");

            migrationBuilder.AlterColumn<string>(
                name: "Noi_Dung",
                schema: "dbo",
                table: "ThongBao_Tin",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
