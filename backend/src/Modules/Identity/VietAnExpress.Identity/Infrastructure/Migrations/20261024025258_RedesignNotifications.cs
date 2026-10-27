using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class RedesignNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Doi_Tuong",
                schema: "dbo",
                table: "ThongBao",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "all");

            migrationBuilder.AddColumn<bool>(
                name: "Ghim",
                schema: "dbo",
                table: "ThongBao",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Loai_Khach",
                schema: "dbo",
                table: "ThongBao",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Ngay_Dang",
                schema: "dbo",
                table: "ThongBao",
                type: "datetime",
                nullable: false,
                defaultValueSql: "getdate()");

            migrationBuilder.AddColumn<DateTime>(
                name: "Ngay_Het_Han",
                schema: "dbo",
                table: "ThongBao",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Trang_Thai",
                schema: "dbo",
                table: "ThongBao",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Thông báo cũ: đăng lúc tạo, mọi khách, đã đăng (giá trị mặc định ở trên).
            migrationBuilder.Sql("UPDATE dbo.ThongBao SET Ngay_Dang = Ngay_Tao;");

            migrationBuilder.CreateTable(
                name: "ThongBao_NguoiNhan",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThongBao_ID = table.Column<long>(type: "bigint", nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBao_NguoiNhan", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ThongBao_NguoiNhan_ThongBao_ThongBao_ID",
                        column: x => x.ThongBao_ID,
                        principalSchema: "dbo",
                        principalTable: "ThongBao",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThongBao_Tin",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThongBao_ID = table.Column<long>(type: "bigint", nullable: false),
                    Thu_Tu = table.Column<int>(type: "int", nullable: false),
                    Tieu_De = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Noi_Dung = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Hinh_Anh = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Duong_Dan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBao_Tin", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ThongBao_Tin_ThongBao_ThongBao_ID",
                        column: x => x.ThongBao_ID,
                        principalSchema: "dbo",
                        principalTable: "ThongBao",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_Trang_Thai_Ngay_Dang",
                schema: "dbo",
                table: "ThongBao",
                columns: new[] { "Trang_Thai", "Ngay_Dang" });

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_NguoiNhan_Khach",
                schema: "dbo",
                table: "ThongBao_NguoiNhan",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "UX_ThongBao_NguoiNhan",
                schema: "dbo",
                table: "ThongBao_NguoiNhan",
                columns: new[] { "ThongBao_ID", "CustomerID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_Tin_ThongBao",
                schema: "dbo",
                table: "ThongBao_Tin",
                columns: new[] { "ThongBao_ID", "Thu_Tu" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThongBao_NguoiNhan",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ThongBao_Tin",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_ThongBao_Trang_Thai_Ngay_Dang",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.DropColumn(
                name: "Doi_Tuong",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.DropColumn(
                name: "Ghim",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.DropColumn(
                name: "Loai_Khach",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.DropColumn(
                name: "Ngay_Dang",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.DropColumn(
                name: "Ngay_Het_Han",
                schema: "dbo",
                table: "ThongBao");

            migrationBuilder.DropColumn(
                name: "Trang_Thai",
                schema: "dbo",
                table: "ThongBao");
        }
    }
}
