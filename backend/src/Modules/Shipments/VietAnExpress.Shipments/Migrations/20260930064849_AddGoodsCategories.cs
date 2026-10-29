using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class AddGoodsCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NhomHangHoa",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: true),
                    Ten_Nhom = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Yeu_Thich = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Thu_Tu = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhomHangHoa", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhomHangHoa_CustomerID_Ten_Nhom",
                schema: "dbo",
                table: "NhomHangHoa",
                columns: new[] { "CustomerID", "Ten_Nhom" });

            // Nhóm chung Việt An cho mọi khách (CustomerID NULL); khách tự thêm nhóm riêng trên portal.
            migrationBuilder.Sql("""
                INSERT INTO dbo.NhomHangHoa (CustomerID, Ten_Nhom, Yeu_Thich, Thu_Tu) VALUES
                (NULL, N'Quần áo, giày dép', 0, 1),
                (NULL, N'Túi xách, phụ kiện thời trang', 0, 2),
                (NULL, N'Trang sức, đồng hồ', 0, 3),
                (NULL, N'Mỹ phẩm, hóa mỹ phẩm', 0, 4),
                (NULL, N'Thực phẩm khô, đặc sản', 0, 5),
                (NULL, N'Thực phẩm chức năng', 0, 6),
                (NULL, N'Thuốc, dược phẩm', 0, 7),
                (NULL, N'Đồ điện tử', 0, 8),
                (NULL, N'Linh kiện, phụ tùng máy móc', 0, 9),
                (NULL, N'Hàng có pin', 0, 10),
                (NULL, N'Chất lỏng, dầu, nước hoa', 0, 11),
                (NULL, N'Sách, văn phòng phẩm', 0, 12),
                (NULL, N'Đồ gia dụng, nhà bếp', 0, 13),
                (NULL, N'Đồ chơi', 0, 14),
                (NULL, N'Hàng thủ công mỹ nghệ', 0, 15),
                (NULL, N'Vải, nguyên phụ liệu may', 0, 16),
                (NULL, N'Hàng mẫu (sample)', 0, 17),
                (NULL, N'Quà tặng cá nhân', 0, 18);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhomHangHoa",
                schema: "dbo");
        }
    }
}
