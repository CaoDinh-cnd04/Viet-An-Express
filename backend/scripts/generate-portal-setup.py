"""Build a standalone SQL baseline from final EF DDL, without touching a database.

Export input first from backend/:
  dotnet run --project tools/IntegrityProbe -- --export-portal-ddl ../backup/sql-current
Run this script from any directory. The output is for imported legacy tables plus
the current portal schema; old portal installations must use migrations instead.
"""
import argparse
import pathlib
import re
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source-dir', type=pathlib.Path, default=ROOT / 'backup/sql-current')
parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'backend/scripts/setup-portal-current.sql')
args = parser.parse_args()
modules = ('Shipments', 'Ecommerce', 'Identity')
version = next(p.attrib['Version'] for p in ET.parse(ROOT / 'backend/Directory.Packages.props').iter('PackageVersion') if p.attrib.get('Include') == 'Microsoft.EntityFrameworkCore.SqlServer')

def literal(value):
    return "N'" + str(value).replace("'", "''") + "'"

def execute(sql):
    return 'EXEC(' + literal(sql.strip()) + ');'

output = []
def add(sql=''):
    output.append(sql.strip())

add('''-- Việt An Express: cấu trúc portal hiện hành — 15/10/2026.
-- Chọn đúng database vietanexpress trong SSMS rồi chạy TOÀN BỘ file (F5).
-- Đây là script dựng schema hiện hành sau khi đã nạp các bảng hệ thống cũ.
-- Không tạo lại các bảng đã nạp; chỉ thêm 10 bảng portal, các cột/index cần thiết
-- trên 4 bảng cũ, danh mục kênh mặc định và 3 bảng lịch sử migration.
-- Không chứa DROP TABLE, không xóa/ghi đè dữ liệu nghiệp vụ có sẵn.
-- Database đang dùng cấu trúc portal đời trước: dùng create-portal-tables.sql
-- hoặc update-database.ps1 để chuyển dữ liệu, không dùng file baseline này.
-- Script chạy lại được; schema không phù hợp sẽ báo lỗi và rollback toàn bộ.
-- Các lỗi ghi đồng thời trong báo cáo audit chưa được sửa bằng script này.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51000, N'Hãy chọn database ứng dụng trước khi chạy script.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock @Resource=N'VietAnPortalSchemaSetup', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
    IF @lockResult < 0 THROW 51000, N'Có phiên khác đang dựng schema portal, thử lại sau.', 1;

-- A. Điều kiện: các bảng được nạp từ hệ thống cũ phải có sẵn.''')
legacy = ('MaVanDon', 'MaVanDon_ChiTietHang', 'MaVanDon_PCS_DIM', 'TCustomer', 'USERS', 'VungXauVungXa', 'Catalog_HsCode', 'TCustomer_NguoiLienLac', 'TContact', 'TCustomer_MyTracking', 'TCustomer_MyTracking_Banner', 'Countries')
for table in legacy:
    add(f"IF OBJECT_ID(N'dbo.{table}', N'U') IS NULL\n    THROW 51000, N'Thiếu bảng đã nạp dbo.{table}. Nạp bảng cũ trước rồi chạy lại.', 1;")
for schema, table in (('shipments','OrderDrafts'),('shipments','Shipments'),('dbo','TaiKhoanNhanVien'),('dbo','MyTrackingCauHinh'),('dbo','GopY'),('dbo','GopY_HinhAnh'),('dbo','SuCo'),('dbo','SuCo_TraoDoi'),('dbo','YeuCauSuaNguoiGui')):
    add(f"IF OBJECT_ID(N'{schema}.{table}', N'U') IS NOT NULL\n    THROW 51000, N'Còn bảng portal đời trước {schema}.{table}. Dùng migration để bảo toàn dữ liệu trước.', 1;")

add('-- B. Bốn bảng hệ thống cũ có thay đổi cấu trúc.')
expected_columns = []
def expect(table, name, store_type, nullable, identity=False):
    match = re.fullmatch(r'(\w+)(?:\((max|\d+)(?:\s*,\s*(\d+))?\))?',store_type)
    kind, size, scale = match.groups()
    length = -1 if size == 'max' else int(size) * (2 if kind in ('nvarchar','nchar') else 1) if size and kind in ('nvarchar','nchar','varchar','char','binary','varbinary') else None
    precision = int(size) if kind in ('decimal','numeric') and size else None
    expected_columns.append((table, name, kind, length, precision, int(scale or 0) if precision else None, int(nullable), int(identity)))

def add_column(table, name, kind, nullable=True, default=None):
    statement = f'ALTER TABLE [dbo].[{table}] ADD [{name}] {kind} ' + ('NULL' if nullable else 'NOT NULL')
    if default:
        statement += f' CONSTRAINT [DF_{table}_{name}_Portal] DEFAULT ({default})'
    add(f"IF COL_LENGTH(N'dbo.{table}', N'{name}') IS NULL\n    {execute(statement)}")
    expect(table,name,kind,nullable)

for name, kind in (('ConsigneeIossNo','nvarchar(12)'),('ConsigneeEoriNo','nvarchar(17)'),('VA_Bill','nvarchar(30)')):
    add_column('MaVanDon',name,kind)
add_column('TCustomer_NguoiLienLac','Quyen','nvarchar(1000)')
for name, kind, nullable, default in (('Duong_Dan','varchar(60)',True,None),('Da_Xuat_Ban','bit',False,'0'),('Hinh_Nen','nvarchar(max)',True,None),('Cau_Hinh_Bo_Sung','nvarchar(max)',True,None),('CreateDate','datetime',True,'GETDATE()'),('ModifyDate','datetime',True,None)):
    add_column('TCustomer_MyTracking',name,kind,nullable,default)
add_column('TCustomer_MyTracking_Banner','Cau_Hinh_Bo_Sung','nvarchar(max)')
expanded = {
    'TCustomer_MyTracking': ('UuDai_TieuDe','UuDai_NoiDung','CompanyName','Address','Tel','Logo','CongTruyenThong_01','CongTruyenThong_02','CongTruyenThong_03','CongTinNhan_01','CongTinNhan_02'),
    'TCustomer_MyTracking_Banner': ('Str1','Url_Hinh','Link','Str2','Str3')
}
for table, columns in expanded.items():
    for column in columns:
        add(f"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{column}' AND system_type_id=231)\n    THROW 51000, N'Cột gốc dbo.{table}.{column} phải có kiểu nvarchar.', 1;")
        add(f"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{column}' AND (max_length<>-1 OR is_nullable=0))\n    {execute(f'ALTER TABLE [dbo].[{table}] ALTER COLUMN [{column}] nvarchar(max) NULL')}")
        expect(table,column,'nvarchar(max)',True)

add('-- C. Mười bảng portal hiện hành, khóa, index và danh mục kênh.')
expected_constraints = []
expected_indexes = []
expected_foreign_keys = []
portal_tables = []
for module in modules:
    ddl = (args.source_dir / f'{module}.create.sql').read_text(encoding='utf-8-sig')
    add(f'-- Module {module}')
    for batch in re.split(r'(?m)^\s*GO\s*$',ddl):
        batch = batch.strip()
        if not batch:
            continue
        table_match = re.match(r'CREATE TABLE \[dbo\]\.\[(\w+)\]', batch)
        index_match = re.match(r'CREATE (UNIQUE )?INDEX \[(\w+)\] ON \[dbo\]\.\[(\w+)\] \((.*?)\)(?: WHERE (.*?))?;', batch, re.S)
        if table_match:
            table = table_match[1]
            portal_tables.append(table)
            add(f"IF OBJECT_ID(N'dbo.{table}',N'U') IS NULL\n    {execute(batch)}")
            for line in batch.splitlines():
                column = re.match(r'\s*\[(\w+)\] (\w+(?:\([^)]*\))?) (NOT NULL|NULL)(.*)',line)
                if column:
                    expect(table,column[1],column[2],column[3]=='NULL','IDENTITY' in column[4])
            for name, kind in re.findall(r'CONSTRAINT \[(\w+)\] (PRIMARY KEY|FOREIGN KEY|UNIQUE|CHECK)',batch):
                expected_constraints.append((table,name,{'PRIMARY KEY':'PK','FOREIGN KEY':'F','UNIQUE':'UQ','CHECK':'C'}[kind]))
            for name, _, keys in re.findall(r'CONSTRAINT \[(\w+)\] (PRIMARY KEY|UNIQUE) \(([^)]+)\)',batch):
                expected_indexes.append((table,name,1,keys.replace('[','').replace(']','').replace(' ',''),''))
            for name, column, parent, parent_column, action in re.findall(r'CONSTRAINT \[(\w+)\] FOREIGN KEY \(\[(\w+)\]\) REFERENCES \[dbo\]\.\[(\w+)\] \(\[(\w+)\]\) ON DELETE (CASCADE|NO ACTION)',batch):
                expected_foreign_keys.append((table,name,column,parent,parent_column,1 if action=='CASCADE' else 0))
        elif index_match:
            unique, name, table, keys, filter_sql = index_match.groups()
            add(f"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{name}')\n    {execute(batch)}")
            expected_indexes.append((table,name,int(bool(unique)),keys.replace('[','').replace(']','').replace(' ',''),filter_sql or ''))
        elif batch.startswith('IF SCHEMA_ID'):
            add(batch)
        elif batch.startswith('CREATE SEQUENCE'):
            add(f"IF OBJECT_ID(N'shipments.LegacyOrderNumberSequence', N'SO') IS NULL\n    {execute(batch)}")
        elif 'INSERT INTO [dbo].[KenhTMDT]' in batch:
            seed = re.search(r'INSERT INTO \[dbo\]\.\[KenhTMDT\] \((.*?)\)\s*VALUES (.*?);',batch,re.S)
            for row in re.findall(r'\((\d+,.*?)(?=\),?\s*(?:\(|$))',seed[2],re.S):
                row = row.strip()
                # Model seeds have scalar CAST(...) values; split at row boundaries only.
                channel_id = int(row.split(',',1)[0])
                code = re.match(r"\d+, '[^']+', '([^']+)'",row)[1]
                add(f"IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID={channel_id} AND Ma_Kenh<>{literal(code)}) OR (Ma_Kenh={literal(code)} AND ID<>{channel_id}))\n    THROW 51000, N'ID hoặc mã kênh {code} đã được dùng khác danh mục trong code; cần đối chiếu.', 1;")
                add(f"IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID={channel_id})\n    {execute(f'INSERT INTO dbo.KenhTMDT ({seed[1]}) VALUES ({row});')}")
        else:
            raise ValueError(f'Unsupported EF DDL batch: {batch[:100]}')
if len(portal_tables) != 10:
    raise ValueError(f'Expected 10 current portal tables, found {portal_tables}')

add('-- D. Index trên hai bảng MyTracking cũ; dừng nếu dữ liệu trùng.')
add("IF EXISTS (SELECT CustomerID FROM dbo.TCustomer_MyTracking GROUP BY CustomerID HAVING COUNT_BIG(*)>1)\n    THROW 51000, N'MyTracking có nhiều trang cho một khách; không tự ghi đè hoặc gộp.', 1;")
for table,name,unique,keys,filter_sql in (
    ('TCustomer_MyTracking','UX_TCustomer_MyTracking_CustomerID',True,'CustomerID',''),
    ('TCustomer_MyTracking','UX_TCustomer_MyTracking_Duong_Dan',True,'Duong_Dan','[Duong_Dan] IS NOT NULL'),
    ('TCustomer_MyTracking_Banner','IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder',False,'MyTrackingID,SortOrder','')):
    statement = f"CREATE {'UNIQUE ' if unique else ''}INDEX [{name}] ON dbo.[{table}] (" + ','.join(f'[{k}]' for k in keys.split(',')) + ')' + (' WHERE '+filter_sql if filter_sql else '') + ';'
    add(f"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{name}')\n    {execute(statement)}")
    expected_indexes.append((table,name,int(unique),keys,filter_sql))

add('-- E. Kiểm tra cấu trúc trước khi ghi baseline migration; không chỉ bỏ qua bảng đã có.')
add('DECLARE @expectedColumns TABLE (TableName sysname,ColumnName sysname,TypeName sysname,MaxBytes smallint NULL,PrecisionValue tinyint NULL,ScaleValue tinyint NULL,Nullable bit,IdentityValue bit);')
add('INSERT @expectedColumns VALUES\n' + ',\n'.join('('+','.join('NULL' if v is None else str(v) if isinstance(v,int) else literal(v) for v in row)+')' for row in expected_columns) + ';')
add('''IF EXISTS (
    SELECT 1 FROM @expectedColumns e
    LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.TableName)) AND c.name=e.ColumnName
    LEFT JOIN sys.types t ON t.user_type_id=c.user_type_id
    WHERE c.column_id IS NULL OR t.name<>e.TypeName OR c.is_nullable<>e.Nullable OR c.is_identity<>e.IdentityValue
       OR (e.MaxBytes IS NOT NULL AND c.max_length<>e.MaxBytes)
       OR (e.PrecisionValue IS NOT NULL AND (c.precision<>e.PrecisionValue OR c.scale<>e.ScaleValue))
)
BEGIN
    SELECT e.TableName,e.ColumnName,e.TypeName AS ExpectedType,t.name AS ActualType,e.MaxBytes AS ExpectedMaxBytes,c.max_length AS ActualMaxBytes,e.Nullable AS ExpectedNullable,c.is_nullable AS ActualNullable
    FROM @expectedColumns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.TableName)) AND c.name=e.ColumnName LEFT JOIN sys.types t ON t.user_type_id=c.user_type_id
    WHERE c.column_id IS NULL OR t.name<>e.TypeName OR c.is_nullable<>e.Nullable OR c.is_identity<>e.IdentityValue OR (e.MaxBytes IS NOT NULL AND c.max_length<>e.MaxBytes) OR (e.PrecisionValue IS NOT NULL AND (c.precision<>e.PrecisionValue OR c.scale<>e.ScaleValue));
    THROW 51000, N'Cấu trúc bảng có sẵn chưa khớp code. Xem danh sách cột phía trên; chưa ghi baseline.', 1;
END;''')
for table,name,kind in expected_constraints:
    add(f"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{name}' AND type=N'{kind}')\n    THROW 51000, N'Thiếu ràng buộc {name} trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;")
    if kind=='C':
        add(f"IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{name}' AND (is_disabled=1 OR is_not_trusted=1))\n    THROW 51000, N'Ràng buộc {name} đang tắt hoặc chưa trusted.', 1;")
for table,name,column,parent,parent_column,action in expected_foreign_keys:
    add(f"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id WHERE fk.parent_object_id=OBJECT_ID(N'dbo.{table}') AND fk.name=N'{name}' AND fk.referenced_object_id=OBJECT_ID(N'dbo.{parent}') AND COL_NAME(fc.parent_object_id,fc.parent_column_id)=N'{column}' AND COL_NAME(fc.referenced_object_id,fc.referenced_column_id)=N'{parent_column}' AND fk.delete_referential_action={action} AND fk.is_disabled=0 AND fk.is_not_trusted=0 AND (SELECT COUNT(*) FROM sys.foreign_key_columns WHERE constraint_object_id=fk.object_id)=1)\n    THROW 51000, N'Khóa ngoại {name} không đúng định nghĩa hoặc chưa bật/trusted.', 1;")
add('DECLARE @expectedIndexes TABLE (TableName sysname,IndexName sysname,UniqueValue bit,KeyColumns nvarchar(1000),FilterSql nvarchar(1000));')
add('INSERT @expectedIndexes VALUES\n'+',\n'.join('('+','.join(str(v) if isinstance(v,int) else literal(v) for v in row)+')' for row in expected_indexes)+';')
add('''IF EXISTS (
    SELECT 1 FROM @expectedIndexes e
    LEFT JOIN sys.indexes i ON i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.TableName)) AND i.name=e.IndexName
    WHERE i.index_id IS NULL OR i.is_unique<>e.UniqueValue OR i.is_disabled=1 OR i.is_hypothetical=1
       OR e.KeyColumns<>STUFF((SELECT N','+c.name FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 ORDER BY ic.key_ordinal FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,N'')
       OR UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(i.filter_definition,N''),N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''))<>UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(e.FilterSql,N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''))
)
    THROW 51000, N'Index có sẵn khác định nghĩa trong code hoặc bị vô hiệu hóa; chưa ghi baseline.', 1;''')

add('-- F. Lịch sử EF: ghi baseline của schema đã kiểm tra, không chạy lại các bước tạo/xóa bảng đời trước.')
for module in modules:
    schema = module.lower()
    migrations = sorted(p.stem for p in (ROOT/f'backend/src/Modules/{module}/VietAnExpress.{module}/Infrastructure/Migrations').glob('*.cs') if re.fullmatch(r'\d{14}_[^.]+',p.stem))
    add(f"IF SCHEMA_ID(N'{schema}') IS NULL {execute(f'CREATE SCHEMA [{schema}]')}")
    add(f"IF OBJECT_ID(N'{schema}.__EFMigrationsHistory',N'U') IS NULL\n    {execute(f'CREATE TABLE [{schema}].[__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));')}")
    allowed=','.join(literal(m) for m in migrations)
    add(execute(f"IF EXISTS (SELECT 1 FROM [{schema}].[__EFMigrationsHistory] WHERE MigrationId NOT IN ({allowed})) THROW 51000, N'Lịch sử {module} chứa migration không có trong bản code này; không hạ phiên bản.', 1;"))
    for migration in migrations:
        add(execute(f"IF NOT EXISTS (SELECT 1 FROM [{schema}].[__EFMigrationsHistory] WHERE MigrationId={literal(migration)}) INSERT [{schema}].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES ({literal(migration)},{literal(version)});"))
add('''    COMMIT TRANSACTION;
    PRINT N'Đã hoàn tất schema portal hiện hành. Các bảng đã nạp không thay đổi cấu trúc được giữ nguyên.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;''')
args.output.parent.mkdir(parents=True,exist_ok=True)
args.output.write_text('\n\n'.join(output)+'\n',encoding='utf-8-sig')
print(f'Wrote {args.output}: {len(portal_tables)} portal tables, {len(expected_columns)} checked columns, {len(expected_indexes)} checked indexes; EF {version}')
