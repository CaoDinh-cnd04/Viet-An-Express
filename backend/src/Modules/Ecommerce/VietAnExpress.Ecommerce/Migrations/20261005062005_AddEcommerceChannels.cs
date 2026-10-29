using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Ecommerce.Shared;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VietAnExpress.Ecommerce.Migrations
{
    /// <inheritdoc />
    internal partial class AddEcommerceChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "KenhTMDT",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false),
                    Ma_Kenh = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Ten_Kenh = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kieu_Ket_Noi = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Website = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Dang_Hoat_Dong = table.Column<bool>(type: "bit", nullable: false),
                    Day_Tracking = table.Column<bool>(type: "bit", nullable: false),
                    Thu_Tu = table.Column<int>(type: "int", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KenhTMDT", x => x.ID);
                    table.UniqueConstraint("AK_KenhTMDT_Ma_Kenh", x => x.Ma_Kenh);
                });

            migrationBuilder.CreateTable(
                name: "KetNoiTMDT",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Ma_Kenh = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Ma_Shop = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Ten_Shop = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ten_Mien_Shop = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Shop_Cipher = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Khu_Vuc = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Tien_Te = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: true),
                    Quyen_Truy_Cap = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    Access_Token_Ma_Hoa = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    Access_Token_Het_Han = table.Column<DateTime>(type: "datetime", nullable: true),
                    Refresh_Token_Ma_Hoa = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    Refresh_Token_Het_Han = table.Column<DateTime>(type: "datetime", nullable: true),
                    Webhook_IDs = table.Column<string>(type: "varchar(2000)", unicode: false, maxLength: 2000, nullable: true),
                    Trang_Thai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Ngay_Ket_Noi = table.Column<DateTime>(type: "datetime", nullable: false),
                    Dong_Bo_Lan_Cuoi = table.Column<DateTime>(type: "datetime", nullable: true),
                    Loi_Gan_Nhat = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ngay_Loi = table.Column<DateTime>(type: "datetime", nullable: true),
                    Ngay_Ngat_Ket_Noi = table.Column<DateTime>(type: "datetime", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    ModifyDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KetNoiTMDT", x => x.ID);
                    table.CheckConstraint("CK_KetNoiTMDT_Trang_Thai", "[Trang_Thai] IN ('active', 'expired', 'error', 'revoked')");
                    table.ForeignKey(
                        name: "FK_KetNoiTMDT_KenhTMDT_Ma_Kenh",
                        column: x => x.Ma_Kenh,
                        principalSchema: "dbo",
                        principalTable: "KenhTMDT",
                        principalColumn: "Ma_Kenh",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "KenhTMDT",
                columns: new[] { "ID", "Kieu_Ket_Noi", "Ma_Kenh", "CreateDate", "Dang_Hoat_Dong", "ModifyDate", "Ten_Kenh", "Thu_Tu", "Day_Tracking", "Website" },
                values: new object[,]
                {
                    { 1, "oauth2", "shopify", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Shopify", 1, true, "https://www.shopify.com" },
                    { 2, "oauth2", "tiktok", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "TikTok Shop", 2, true, "https://seller.tiktokshop.com" },
                    { 3, "oauth2", "amazon", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Amazon", 3, true, "https://sellercentral.amazon.com" },
                    { 4, "oauth2", "ebay", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "eBay", 4, true, "https://www.ebay.com" },
                    { 5, "oauth2", "etsy", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Etsy", 5, true, "https://www.etsy.com" },
                    { 6, "api_key", "woocommerce", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "WooCommerce", 6, true, "https://woocommerce.com" },
                    { 7, "oauth2", "shopee", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Shopee", 7, false, "https://banhang.shopee.vn" },
                    { 8, "oauth2", "lazada", new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "Lazada", 8, false, "https://sellercenter.lazada.vn" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_KetNoiTMDT_Ma_Kenh_Ma_Shop",
                schema: "dbo",
                table: "KetNoiTMDT",
                columns: new[] { "Ma_Kenh", "Ma_Shop" });

            migrationBuilder.CreateIndex(
                name: "UX_KetNoiTMDT_CustomerID_Ma_Kenh_Ma_Shop",
                schema: "dbo",
                table: "KetNoiTMDT",
                columns: new[] { "CustomerID", "Ma_Kenh", "Ma_Shop" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KetNoiTMDT",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KenhTMDT",
                schema: "dbo");
        }
    }
}
