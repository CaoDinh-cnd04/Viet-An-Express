using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class MoveSupportToTContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Yêu cầu hỗ trợ & sự cố chuyển sang bảng liên hệ / form của hệ thống cũ (FormType "Portal - …") để phần mềm cũ hiện và xử lý.
            // Lượt trao đổi (dòng con La_Trao_Doi = 1) không chuyển — đã bỏ chức năng trao đổi.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.TContact', N'U') IS NULL
                    THROW 51000, N'Thiếu bảng dbo.TContact. Chạy backend/scripts/legacy-tables.sql trước.', 1;
                INSERT INTO dbo.TContact (Name, Phone, Email, Status, CompanyName, Title, Content, KeyWord, FormType, RegDate, ModifyDate,
                    FileID, FileID1, FileID2, FileID3, Des01, Des02, Des03, Des04, Des05, CustID)
                SELECT Nguoi_Yeu_Cau, Lien_He, CASE WHEN Lien_He LIKE N'%_@_%._%' THEN Lien_He END,
                    CASE Trang_Thai WHEN 'doing' THEN 1 WHEN 'waitc' THEN 2 WHEN 'done' THEN 3 ELSE 0 END,
                    Ten_Cong_Ty, COALESCE(NULLIF(Tieu_De, N''), Loai_Su_Co), Mo_Ta, Loai_Su_Co,
                    CASE Loai WHEN 'ho_tro' THEN N'Portal - Hỗ trợ' WHEN 'gop_y' THEN N'Portal - Góp ý' ELSE N'Portal - Sự cố' END,
                    Ngay_Tao, ISNULL(Ngay_Cap_Nhat, Ngay_Tao), 0, 0, 0, 0,
                    Ma_Van_Don, Muc_Do, Nguoi_Nhan, Quoc_Gia, CONVERT(nvarchar(30), StaffID), CustomerID
                FROM dbo.SuCo
                WHERE La_Trao_Doi = 0
                ORDER BY ID;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_SuCo_SuCo_ID_Cha",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropTable(name: "SuCo", schema: "dbo");

            // Sửa thông tin người gửi nay là yêu cầu hỗ trợ thường (CS sửa ở phần mềm cũ).

            migrationBuilder.DropTable(
                name: "YeuCauSuaNguoiGui",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lịch sử trao đổi và cấu trúc dbo.SuCo không dựng lại được từ dbo.TContact — khôi phục từ bản sao lưu nếu cần.
            throw new NotSupportedException(
                "Không hoàn tác được MoveSupportToTContact: khôi phục dbo.SuCo / dbo.YeuCauSuaNguoiGui từ backup/2026-10-15_truoc-TContact.sql.");
        }
    }
}
