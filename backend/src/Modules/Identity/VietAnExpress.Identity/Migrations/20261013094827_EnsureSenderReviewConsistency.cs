using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Identity.Shared;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class EnsureSenderReviewConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_YeuCauSuaNguoiGui_Pending_CustomerID",
                schema: "dbo",
                table: "YeuCauSuaNguoiGui",
                column: "CustomerID",
                unique: true,
                filter: "[Trang_Thai] = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_YeuCauSuaNguoiGui_Pending_CustomerID",
                schema: "dbo",
                table: "YeuCauSuaNguoiGui");
        }
    }
}
