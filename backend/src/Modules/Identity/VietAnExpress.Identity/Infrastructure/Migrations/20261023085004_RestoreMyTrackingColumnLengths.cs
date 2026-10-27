using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <summary>
    /// Trả các cột MyTracking không cần độ dài lớn về nvarchar(250) như bảng gốc của hệ thống cũ (người dùng yêu cầu 23/10/2026).
    /// Giữ nvarchar(max) cho cột cần: Logo, Url_Hinh (ảnh base64), UuDai_NoiDung (≤ 2000), Address (≤ 500),
    /// CompanyName (theo InvoiceCustomerName ≤ 500), Link (link đích dài).
    /// Có giá trị dài hơn 250 ký tự thì dừng, không cắt dữ liệu.
    /// </summary>
    internal partial class RestoreMyTrackingColumnLengths : Migration
    {
        private static readonly (string Table, string Column)[] Columns =
        [
            ("TCustomer_MyTracking", "UuDai_TieuDe"),
            ("TCustomer_MyTracking", "Tel"),
            ("TCustomer_MyTracking", "CongTinNhan_01"),
            ("TCustomer_MyTracking", "CongTinNhan_02"),
            ("TCustomer_MyTracking", "CongTruyenThong_01"),
            ("TCustomer_MyTracking", "CongTruyenThong_02"),
            ("TCustomer_MyTracking", "CongTruyenThong_03"),
            ("TCustomer_MyTracking_Banner", "Str1"),
            ("TCustomer_MyTracking_Banner", "Str2"),
            ("TCustomer_MyTracking_Banner", "Str3")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Columns)
            {
                migrationBuilder.Sql($"""
                    IF EXISTS (SELECT 1 FROM [dbo].[{table}] WHERE LEN([{column}]) > 250)
                        THROW 51001, N'dbo.{table}.{column} có giá trị dài hơn 250 ký tự — sửa dữ liệu trước khi chạy lại.', 1;
                    ALTER TABLE [dbo].[{table}] ALTER COLUMN [{column}] nvarchar(250) NULL;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Columns)
                migrationBuilder.Sql($"ALTER TABLE [dbo].[{table}] ALTER COLUMN [{column}] nvarchar(max) NULL;");
        }
    }
}
