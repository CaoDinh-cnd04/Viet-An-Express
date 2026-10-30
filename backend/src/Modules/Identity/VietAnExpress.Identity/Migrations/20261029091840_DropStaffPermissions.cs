using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Migrations
{
    /// <inheritdoc />
    internal partial class DropStaffPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tài khoản con không phân quyền riêng nữa (mọi quyền trừ quản lý tài khoản con / MyTracking / đổi mật khẩu,
            // chỉ thấy đơn mình tạo) → bỏ cột Quyen portal đã thêm, trả bảng cũ về đúng cấu trúc hệ thống cũ.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.TCustomer_NguoiLienLac', N'Quyen') IS NOT NULL
    ALTER TABLE dbo.TCustomer_NguoiLienLac DROP COLUMN Quyen;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.TCustomer_NguoiLienLac', N'Quyen') IS NULL
    ALTER TABLE dbo.TCustomer_NguoiLienLac ADD Quyen nvarchar(1000) NULL;");
        }
    }
}
