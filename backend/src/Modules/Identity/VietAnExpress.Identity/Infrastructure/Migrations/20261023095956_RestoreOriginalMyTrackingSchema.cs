using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VietAnExpress.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class RestoreOriginalMyTrackingSchema : Migration
    {
        internal const string RestoreSql = """
            IF OBJECT_ID(N'dbo.TCustomer_MyTracking', N'U') IS NULL
                OR OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner', N'U') IS NULL
                THROW 51001, N'Thiếu hai bảng MyTracking gốc.', 1;

            DECLARE @table sysname, @column sysname, @length int, @nullable bit, @sql nvarchar(max);
            DECLARE columns_to_restore CURSOR LOCAL FAST_FORWARD FOR
                SELECT t.name, c.name, CASE WHEN t.name=N'TCustomer_MyTracking_Banner' AND c.name=N'TypeFile' THEN 50 ELSE 250 END, c.is_nullable
                FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id
                WHERE s.name=N'dbo' AND t.name IN(N'TCustomer_MyTracking',N'TCustomer_MyTracking_Banner') AND c.system_type_id=231;
            OPEN columns_to_restore;
            FETCH NEXT FROM columns_to_restore INTO @table,@column,@length,@nullable;
            WHILE @@FETCH_STATUS=0
            BEGIN
                SET @sql=N'IF EXISTS(SELECT 1 FROM dbo.'+QUOTENAME(@table)+N' WHERE DATALENGTH('+QUOTENAME(@column)+N')>'+CONVERT(nvarchar(10),@length*2)+N')
                    THROW 51001,N''MyTracking có giá trị vượt độ dài cột gốc; chuyển ảnh thành tệp hoặc sửa nội dung trước. Không cắt dữ liệu.'',1;';
                EXEC sys.sp_executesql @sql;
                IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.'+@table) AND name=@column AND max_length<>@length*2)
                BEGIN
                    SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' ALTER COLUMN '+QUOTENAME(@column)+N' nvarchar('+CONVERT(nvarchar(10),@length)+N')'+CASE WHEN @nullable=1 THEN N' NULL' ELSE N' NOT NULL' END;
                    EXEC sys.sp_executesql @sql;
                END;
                FETCH NEXT FROM columns_to_restore INTO @table,@column,@length,@nullable;
            END;
            CLOSE columns_to_restore;
            DEALLOCATE columns_to_restore;

            IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UX_TCustomer_MyTracking_CustomerID')
                DROP INDEX UX_TCustomer_MyTracking_CustomerID ON dbo.TCustomer_MyTracking;
            IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder')
                DROP INDEX IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder ON dbo.TCustomer_MyTracking_Banner;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(RestoreSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Không tự nới lại cấu trúc MyTracking; khôi phục từ bản sao lưu nếu cần.");
    }
}
