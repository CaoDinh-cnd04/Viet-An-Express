using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations;

/// <summary>Chuyển cấu hình và ảnh vào hai bảng cũ, kiểm tra số dòng rồi mới xóa bảng cấu hình portal.</summary>
internal partial class MoveMyTrackingToLegacyTables : Migration
{
    private static readonly (string Path, string Column)[] PageFields =
    [
        ("$.title", "UuDai_TieuDe"), ("$.description", "UuDai_NoiDung"),
        ("$.brand.companyName", "CompanyName"), ("$.brand.address", "Address"),
        ("$.brand.phone", "Tel"), ("$.brand.logo", "Logo"),
        ("$.brand.facebook", "CongTruyenThong_01"), ("$.brand.instagram", "CongTruyenThong_02"),
        ("$.brand.x", "CongTruyenThong_03"), ("$.brand.zalo", "CongTinNhan_01"),
        ("$.brand.whatsapp", "CongTinNhan_02"), ("$.background", "Hinh_Nen")
    ];

    private static readonly (string Path, string Column)[] BannerFields =
    [
        ("$.id", "Str1"), ("$.src", "Url_Hinh"), ("$.linkUrl", "Link"),
        ("$.title", "Str2"), ("$.buttonText", "Str3")
    ];

    // OPENJSON trả nvarchar(max), giữ nguyên cả ảnh base64 > 4.000 ký tự; JSON_VALUE không đáp ứng trường hợp này.
    private static string Text(string json, string path)
    {
        var dot = path.LastIndexOf('.');
        return $"(SELECT j.[value] FROM OPENJSON({json}, '{path[..dot]}') j WHERE j.[key] = N'{path[(dot + 1)..]}' AND j.[type] = 1)";
    }

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.TCustomer_MyTracking', N'U') IS NULL OR OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner', N'U') IS NULL
                THROW 51000, N'Thiếu hai bảng MyTracking cũ. Chạy backend/scripts/legacy-tables.sql trước.', 1;
            IF EXISTS (SELECT CustomerID FROM dbo.TCustomer_MyTracking GROUP BY CustomerID HAVING COUNT_BIG(*) > 1)
                THROW 51000, N'TCustomer_MyTracking có nhiều dòng cùng khách; cần đối chiếu trước khi chuyển cấu hình.', 1;
            IF EXISTS (SELECT 1 FROM dbo.MyTrackingCauHinh s JOIN dbo.TCustomer_MyTracking t ON t.CustomerID = s.CustomerID)
                THROW 51000, N'Khách đã có cấu hình ở cả hai bảng; cần đối chiếu để không ghi đè dữ liệu.', 1;
            IF EXISTS (SELECT 1 FROM dbo.MyTrackingCauHinh WHERE ISJSON(Cau_Hinh) <> 1 OR LEFT(LTRIM(Cau_Hinh), 1) <> '{')
                THROW 51000, N'Cấu hình MyTracking cũ không phải JSON object hợp lệ.', 1;
            IF EXISTS (
                SELECT 1 FROM dbo.MyTrackingCauHinh s
                CROSS APPLY OPENJSON(s.Cau_Hinh) a CROSS APPLY OPENJSON(CASE WHEN a.[type] = 4 THEN a.[value] ELSE N'[]' END) i
                WHERE a.[key] = 'images' AND a.[type] = 4 AND i.[type] <> 5)
                THROW 51000, N'Danh sách ảnh cũ có phần tử không hợp lệ; giữ bảng cũ để kiểm tra.', 1;
            """);

        // Chỉ bổ sung metadata chưa có trong schema cũ; các cột khác giữ nguyên ý nghĩa và tên.
        migrationBuilder.AddColumn<string>(name: "Duong_Dan", schema: "dbo", table: "TCustomer_MyTracking", type: "varchar(60)", unicode: false, maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<bool>(name: "Da_Xuat_Ban", schema: "dbo", table: "TCustomer_MyTracking", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "Hinh_Nen", schema: "dbo", table: "TCustomer_MyTracking", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Cau_Hinh_Bo_Sung", schema: "dbo", table: "TCustomer_MyTracking", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "CreateDate", schema: "dbo", table: "TCustomer_MyTracking", type: "datetime", nullable: true, defaultValueSql: "GETDATE()");
        migrationBuilder.AddColumn<DateTime>(name: "ModifyDate", schema: "dbo", table: "TCustomer_MyTracking", type: "datetime", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Cau_Hinh_Bo_Sung", schema: "dbo", table: "TCustomer_MyTracking_Banner", type: "nvarchar(max)", nullable: true);

        foreach (var (_, column) in PageFields.Where(f => f.Column != "Hinh_Nen"))
            migrationBuilder.AlterColumn<string>(name: column, schema: "dbo", table: "TCustomer_MyTracking", type: "nvarchar(max)", nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(250)", oldMaxLength: 250, oldNullable: true);
        foreach (var (_, column) in BannerFields)
            migrationBuilder.AlterColumn<string>(name: column, schema: "dbo", table: "TCustomer_MyTracking_Banner", type: "nvarchar(max)", nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(250)", oldMaxLength: 250, oldNullable: true);

        migrationBuilder.Sql($"""
            INSERT INTO dbo.TCustomer_MyTracking
                (CustomerID, Duong_Dan, Da_Xuat_Ban, CreateDate, ModifyDate, Cau_Hinh_Bo_Sung, {string.Join(", ", PageFields.Select(f => $"[{f.Column}]"))})
            SELECT s.CustomerID, s.Duong_Dan, s.Da_Xuat_Ban, s.CreateDate, s.ModifyDate, s.Cau_Hinh,
                {string.Join(",\n                ", PageFields.Select(f => Text("s.Cau_Hinh", f.Path)))}
            FROM dbo.MyTrackingCauHinh s;

            INSERT INTO dbo.TCustomer_MyTracking_Banner
                (MyTrackingID, CustomerID, SortOrder, Cau_Hinh_Bo_Sung, {string.Join(", ", BannerFields.Select(f => $"[{f.Column}]"))})
            SELECT t.ID, s.CustomerID, CONVERT(int, i.[key]), i.[value],
                {string.Join(",\n                ", BannerFields.Select(f => Text("i.[value]", f.Path)))}
            FROM dbo.MyTrackingCauHinh s
            JOIN dbo.TCustomer_MyTracking t ON t.CustomerID = s.CustomerID
            CROSS APPLY OPENJSON(s.Cau_Hinh) a
            CROSS APPLY OPENJSON(CASE WHEN a.[type] = 4 THEN a.[value] ELSE N'[]' END) i
            WHERE a.[key] = 'images' AND a.[type] = 4;
            """);

        // Xóa khỏi JSON bổ sung những giá trị đã lưu trong cột; trường lạ / NULL / kiểu khác vẫn được bảo toàn.
        foreach (var (path, column) in PageFields)
            migrationBuilder.Sql($"""
                UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '{path}', NULL)
                FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
                WHERE t.[{column}] IS NOT NULL;
                """);
        foreach (var (path, column) in BannerFields)
            migrationBuilder.Sql($"""
                UPDATE b SET Cau_Hinh_Bo_Sung = JSON_MODIFY(b.Cau_Hinh_Bo_Sung, '{path}', NULL)
                FROM dbo.TCustomer_MyTracking_Banner b JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = b.CustomerID
                WHERE b.[{column}] IS NOT NULL;
                """);
        migrationBuilder.Sql("""
            UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.images', JSON_QUERY(N'[]'))
            FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
            WHERE EXISTS (SELECT 1 FROM OPENJSON(s.Cau_Hinh) a WHERE a.[key] = 'images' AND a.[type] = 4);

            IF (SELECT COUNT_BIG(*) FROM dbo.MyTrackingCauHinh) <>
               (SELECT COUNT_BIG(*) FROM dbo.MyTrackingCauHinh s JOIN dbo.TCustomer_MyTracking t ON t.CustomerID = s.CustomerID
                AND t.Duong_Dan = s.Duong_Dan AND t.Da_Xuat_Ban = s.Da_Xuat_Ban AND t.CreateDate = s.CreateDate
                AND (t.ModifyDate = s.ModifyDate OR (t.ModifyDate IS NULL AND s.ModifyDate IS NULL)))
                THROW 51000, N'Chưa chuyển đủ trang / đường dẫn / trạng thái / ngày giờ; không xóa bảng cũ.', 1;
            IF EXISTS (
                SELECT 1 FROM dbo.MyTrackingCauHinh s JOIN dbo.TCustomer_MyTracking t ON t.CustomerID = s.CustomerID
                WHERE (SELECT COUNT_BIG(*) FROM dbo.TCustomer_MyTracking_Banner b WHERE b.MyTrackingID = t.ID) <>
                      (SELECT COUNT_BIG(*) FROM OPENJSON(s.Cau_Hinh) a
                       CROSS APPLY OPENJSON(CASE WHEN a.[type] = 4 THEN a.[value] ELSE N'[]' END) i WHERE a.[key] = 'images' AND a.[type] = 4))
                THROW 51000, N'Chưa chuyển đủ ảnh; không xóa bảng cũ.', 1;
            """);

        migrationBuilder.CreateIndex(name: "UX_TCustomer_MyTracking_CustomerID", schema: "dbo", table: "TCustomer_MyTracking", column: "CustomerID", unique: true);
        migrationBuilder.CreateIndex(name: "UX_TCustomer_MyTracking_Duong_Dan", schema: "dbo", table: "TCustomer_MyTracking", column: "Duong_Dan", unique: true, filter: "[Duong_Dan] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder", schema: "dbo", table: "TCustomer_MyTracking_Banner", columns: new[] { "MyTrackingID", "SortOrder" });
        migrationBuilder.DropTable(name: "MyTrackingCauHinh", schema: "dbo");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Migration chuyển MyTracking chỉ tiến tới. Khôi phục từ bản sao lưu nếu cần; không tự xóa dữ liệu ở hai bảng hệ thống cũ.");
}
