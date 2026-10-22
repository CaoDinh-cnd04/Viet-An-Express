using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class RemoveRevokedSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bỏ chức năng thu hồi phiên (người dùng yêu cầu 21/10/2026). DB chưa từng tạo bảng thì bỏ qua.
            migrationBuilder.Sql("IF OBJECT_ID(N'dbo.PhienThuHoi', N'U') IS NOT NULL DROP TABLE [dbo].[PhienThuHoi];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhienThuHoi",
                schema: "dbo",
                columns: table => new
                {
                    Ma_Phien = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                    Het_Han = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ngay_Thu_Hoi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhienThuHoi", x => x.Ma_Phien);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhienThuHoi_Het_Han",
                schema: "dbo",
                table: "PhienThuHoi",
                column: "Het_Han");
        }
    }
}
