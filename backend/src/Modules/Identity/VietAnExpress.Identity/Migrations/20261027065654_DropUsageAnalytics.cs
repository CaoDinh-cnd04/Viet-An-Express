using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class DropUsageAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Người dùng bỏ chức năng Thống kê sử dụng (27/10/2026) — xóa bảng cùng dữ liệu lượt truy cập.
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[ThongKeSuDung];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThongKeSuDung",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Visitor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
    }
}
