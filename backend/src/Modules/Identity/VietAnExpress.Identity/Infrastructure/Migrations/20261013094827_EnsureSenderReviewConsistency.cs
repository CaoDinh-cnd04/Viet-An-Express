using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSenderReviewConsistency : Migration
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
