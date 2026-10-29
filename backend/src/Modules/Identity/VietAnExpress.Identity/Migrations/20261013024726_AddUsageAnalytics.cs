using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class AddUsageAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThongKeSuDung",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Visitor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    FlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongKeSuDung", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThongKeSuDung_CreatedAt_Audience_Kind",
                schema: "dbo",
                table: "ThongKeSuDung",
                columns: new[] { "CreatedAt", "Audience", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ThongKeSuDung_FlowId_Mode_Kind",
                schema: "dbo",
                table: "ThongKeSuDung",
                columns: new[] { "FlowId", "Mode", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThongKeSuDung",
                schema: "dbo");
        }
    }
}
