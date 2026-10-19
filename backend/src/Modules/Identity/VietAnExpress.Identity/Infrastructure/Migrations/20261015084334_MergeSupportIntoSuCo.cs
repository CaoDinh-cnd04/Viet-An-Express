using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class MergeSupportIntoSuCo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Trang_Thai",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Ten_Cong_Ty",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<string>(
                name: "Nguoi_Yeu_Cau",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Nguoi_Gui",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Ngay_Cap_Nhat",
                schema: "dbo",
                table: "SuCo",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<string>(
                name: "Muc_Do",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Mo_Ta",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Ma_Van_Don",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Ma_Khach",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Loai_Su_Co",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Lien_He",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerID",
                schema: "dbo",
                table: "SuCo",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "ID_Cha",
                schema: "dbo",
                table: "SuCo",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "La_Trao_Doi",
                schema: "dbo",
                table: "SuCo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Loai",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Tep_Du_Lieu",
                schema: "dbo",
                table: "SuCo",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tep_Kich_Thuoc",
                schema: "dbo",
                table: "SuCo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tep_Loai",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tep_Ten",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tieu_De",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Tu_Viet_An",
                schema: "dbo",
                table: "SuCo",
                type: "bit",
                nullable: true);

            // Sự cố cũ (mọi dòng hiện có) là dòng gốc loại "su_co".
            migrationBuilder.Sql("UPDATE dbo.SuCo SET Loai = 'su_co' WHERE La_Trao_Doi = 0 AND Loai IS NULL;");

            // Chuyển lịch sử trao đổi sang dbo.SuCo thành dòng con (ID_Cha = sự cố gốc), giữ thứ tự cũ.
            migrationBuilder.Sql(@"
INSERT INTO dbo.SuCo (La_Trao_Doi, ID_Cha, Tu_Viet_An, Nguoi_Gui, Mo_Ta, Trang_Thai, Ngay_Tao)
SELECT 1, SuCo_ID, Tu_Viet_An, Nguoi_Viet, Noi_Dung, Trang_Thai_Moi, Ngay_Tao
FROM dbo.SuCo_TraoDoi
ORDER BY ID;");

            migrationBuilder.CreateIndex(
                name: "IX_SuCo_ID_Cha",
                schema: "dbo",
                table: "SuCo",
                column: "ID_Cha");

            migrationBuilder.AddForeignKey(
                name: "FK_SuCo_SuCo_ID_Cha",
                schema: "dbo",
                table: "SuCo",
                column: "ID_Cha",
                principalSchema: "dbo",
                principalTable: "SuCo",
                principalColumn: "ID");

            // Góp ý cũ không giữ (người dùng chọn bỏ); lịch sử trao đổi đã chuyển sang dbo.SuCo.
            migrationBuilder.DropTable(
                name: "GopY_HinhAnh",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SuCo_TraoDoi",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GopY",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GopY",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ten_Cong_Ty = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Lien_He = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ngay_Gui = table.Column<DateTime>(type: "datetime", nullable: false),
                    Ma_Khach = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    Khach_Da_An = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Da_Xem = table.Column<bool>(type: "bit", nullable: false),
                    Noi_Dung = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    So_Sao = table.Column<byte>(type: "tinyint", nullable: true),
                    Ngay_Xem = table.Column<DateTime>(type: "datetime", nullable: true),
                    Nguoi_Gui = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StaffID = table.Column<long>(type: "bigint", nullable: true),
                    Khao_Sat = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GopY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SuCo_TraoDoi",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nguoi_Viet = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Ngay_Tao = table.Column<DateTime>(type: "datetime", nullable: false),
                    Tu_Viet_An = table.Column<bool>(type: "bit", nullable: false),
                    Noi_Dung = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Trang_Thai_Moi = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    SuCo_ID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuCo_TraoDoi", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SuCo_TraoDoi_SuCo_SuCo_ID",
                        column: x => x.SuCo_ID,
                        principalSchema: "dbo",
                        principalTable: "SuCo",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GopY_HinhAnh",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Loai_File = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Du_Lieu = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    GopY_ID = table.Column<long>(type: "bigint", nullable: false),
                    Ten_File = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Kich_Thuoc = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GopY_HinhAnh", x => x.ID);
                    table.ForeignKey(
                        name: "FK_GopY_HinhAnh_GopY_GopY_ID",
                        column: x => x.GopY_ID,
                        principalSchema: "dbo",
                        principalTable: "GopY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GopY_CustomerID_Ngay_Gui",
                schema: "dbo",
                table: "GopY",
                columns: new[] { "CustomerID", "Ngay_Gui" });

            migrationBuilder.CreateIndex(
                name: "IX_GopY_HinhAnh_GopY_ID",
                schema: "dbo",
                table: "GopY_HinhAnh",
                column: "GopY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SuCo_TraoDoi_SuCo_ID",
                schema: "dbo",
                table: "SuCo_TraoDoi",
                column: "SuCo_ID");

            // Lượt trao đổi (dòng con) về lại dbo.SuCo_TraoDoi; yêu cầu hỗ trợ / góp ý không có chỗ trong cấu trúc cũ nên bị bỏ.
            migrationBuilder.Sql(@"
INSERT INTO dbo.SuCo_TraoDoi (SuCo_ID, Tu_Viet_An, Nguoi_Viet, Noi_Dung, Trang_Thai_Moi, Ngay_Tao)
SELECT c.ID_Cha, ISNULL(c.Tu_Viet_An, 0), ISNULL(c.Nguoi_Gui, ''), ISNULL(c.Mo_Ta, ''), c.Trang_Thai, c.Ngay_Tao
FROM dbo.SuCo c JOIN dbo.SuCo p ON p.ID = c.ID_Cha
WHERE c.La_Trao_Doi = 1 AND p.Loai = 'su_co'
ORDER BY c.ID;");
            migrationBuilder.Sql("DELETE FROM dbo.SuCo WHERE La_Trao_Doi = 1;");
            migrationBuilder.Sql("DELETE FROM dbo.SuCo WHERE Loai <> 'su_co';");

            migrationBuilder.DropForeignKey(
                name: "FK_SuCo_SuCo_ID_Cha",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropIndex(
                name: "IX_SuCo_ID_Cha",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "ID_Cha",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "La_Trao_Doi",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Loai",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Tep_Du_Lieu",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Tep_Kich_Thuoc",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Tep_Loai",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Tep_Ten",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Tieu_De",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.DropColumn(
                name: "Tu_Viet_An",
                schema: "dbo",
                table: "SuCo");

            migrationBuilder.AlterColumn<string>(
                name: "Trang_Thai",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ten_Cong_Ty",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nguoi_Yeu_Cau",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nguoi_Gui",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Ngay_Cap_Nhat",
                schema: "dbo",
                table: "SuCo",
                type: "datetime",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Muc_Do",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Mo_Ta",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ma_Van_Don",
                schema: "dbo",
                table: "SuCo",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ma_Khach",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Loai_Su_Co",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Lien_He",
                schema: "dbo",
                table: "SuCo",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerID",
                schema: "dbo",
                table: "SuCo",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
