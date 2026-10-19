<# Tests setup-portal-current.sql only in disposable databases.
   Reads column/default/PK metadata from the existing application's legacy tables;
   copies no customer data. Removes portal additions to simulate original imports.
#>
param(
    [string]$Server='LAPTOP-K91T0OHE\SQLEXPRESS01',
    [string]$SourceDatabase='vietanexpress',
    [string]$SqlPath=(Join-Path $PSScriptRoot 'setup-portal-current.sql')
)
$ErrorActionPreference='Stop'
$setupSql=Get-Content -Raw -Encoding UTF8 -LiteralPath $SqlPath
if($setupSql -match '(?im)^\s*(USE\s|GO\s*$)') { throw 'Test runner requires a single-batch script without USE' }
function New-AuditConnection([string]$Database) {
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$Server; $builder['Initial Catalog']=$Database
    $builder['Integrated Security']=$true; $builder['TrustServerCertificate']=$true
    $builder['Application Name']='VietAnPortalSetupTest'; $builder['Connect Timeout']=10
    $result=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $result.Open(); return $result
}
function Run-Sql($Connection,[string]$Sql) {
    $command=$Connection.CreateCommand(); $command.CommandTimeout=60; $command.CommandText=$Sql
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}
function Scalar($Connection,[string]$Sql) {
    $command=$Connection.CreateCommand(); $command.CommandTimeout=60; $command.CommandText=$Sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Read-Rows($Connection,[string]$Sql) {
    $command=$Connection.CreateCommand(); $command.CommandText=$Sql
    $reader=$command.ExecuteReader()
    try {
        while($reader.Read()) {
            $row=[ordered]@{}
            for($i=0;$i -lt $reader.FieldCount;$i++) { $row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)} }
            [pscustomobject]$row
        }
    } finally { $reader.Dispose(); $command.Dispose() }
}
function Assert-Equal($Expected,$Actual,[string]$Label) {
    if("$Expected" -ne "$Actual") { throw "$Label expected $Expected, got $Actual" }
}
$source=New-AuditConnection $SourceDatabase
try {
    $columns=@(Read-Rows $source @'
SELECT t.name AS TableName,c.name AS ColumnName,ty.name AS TypeName,c.max_length AS MaxBytes,
 c.precision AS PrecisionValue,c.scale AS ScaleValue,c.is_nullable AS Nullable,c.is_identity AS IdentityValue,
 dc.definition AS DefaultSql
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
WHERE s.name='dbo' AND t.name IN ('MaVanDon','MaVanDon_ChiTietHang','MaVanDon_PCS_DIM','TCustomer','USERS','VungXauVungXa','Catalog_HsCode','TCustomer_NguoiLienLac','TContact','TCustomer_MyTracking','TCustomer_MyTracking_Banner','Countries')
ORDER BY t.name,c.column_id;
'@)
    $primaryKeys=@(Read-Rows $source @'
SELECT t.name AS TableName,k.name AS ConstraintName,c.name AS ColumnName,ic.key_ordinal AS Position
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.key_constraints k ON k.parent_object_id=t.object_id AND k.type='PK'
JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=k.unique_index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE s.name='dbo' ORDER BY t.name,ic.key_ordinal;
'@)
} finally { $source.Dispose() }
$added=@{
    MaVanDon=@('ConsigneeIossNo','ConsigneeEoriNo','VA_Bill')
    TCustomer_NguoiLienLac=@('Quyen')
    TCustomer_MyTracking=@('Duong_Dan','Da_Xuat_Ban','Hinh_Nen','Cau_Hinh_Bo_Sung','CreateDate','ModifyDate')
    TCustomer_MyTracking_Banner=@('Cau_Hinh_Bo_Sung')
}
$expanded=@{
    TCustomer_MyTracking=@('UuDai_TieuDe','UuDai_NoiDung','CompanyName','Address','Tel','Logo','CongTruyenThong_01','CongTruyenThong_02','CongTruyenThong_03','CongTinNhan_01','CongTinNhan_02')
    TCustomer_MyTracking_Banner=@('Str1','Url_Hinh','Link','Str2','Str3')
}
$legacyDdl=@()
foreach($group in ($columns | Group-Object TableName)) {
    $table=$group.Name; $definitions=@()
    foreach($column in $group.Group) {
        if($added.ContainsKey($table) -and $column.ColumnName -in $added[$table]) { continue }
        $kind=$column.TypeName
        if($kind -in @('nvarchar','nchar','varchar','char','varbinary','binary')) {
            $length=if($expanded.ContainsKey($table) -and $column.ColumnName -in $expanded[$table]) { 250 } elseif($column.MaxBytes -eq -1) { 'max' } elseif($kind -in @('nvarchar','nchar')) { $column.MaxBytes/2 } else { $column.MaxBytes }
            $kind+="($length)"
        } elseif($kind -in @('decimal','numeric')) { $kind+='('+ $column.PrecisionValue+','+$column.ScaleValue+')' }
        $line='['+$column.ColumnName+'] '+$kind
        if($column.IdentityValue) { $line+=' IDENTITY(1,1)' }
        $line+=if($column.Nullable) { ' NULL' } else { ' NOT NULL' }
        if($column.DefaultSql) { $line+=' DEFAULT '+$column.DefaultSql }
        $definitions+=$line
    }
    $keys=@($primaryKeys | Where-Object TableName -eq $table)
    if($keys.Count) { $definitions+='CONSTRAINT ['+$keys[0].ConstraintName+'] PRIMARY KEY ('+(($keys | ForEach-Object { '['+$_.ColumnName+']' }) -join ',')+')' }
    $legacyDdl+='CREATE TABLE dbo.['+$table+'] ('+($definitions -join ',')+');'
}
Assert-Equal 12 $legacyDdl.Count 'Imported legacy table count'
$master=New-AuditConnection 'master'
$passed=0
try {
    foreach($scenario in @('fresh-and-rerun','missing-import','duplicate-mytracking','obsolete-portal','incompatible-portal','wrong-primary-key','unknown-migration')) {
        $database='vietan_tests_setup_'+[Guid]::NewGuid().ToString('N')
        Run-Sql $master "CREATE DATABASE [$database]"
        $test=$null
        try {
            $test=New-AuditConnection $database
            foreach($ddl in $legacyDdl) { Run-Sql $test $ddl }
            Run-Sql $test @'
INSERT dbo.Countries(CountryName,CountryCode) VALUES (N'Audit country',N'XX');
INSERT dbo.TCustomer_MyTracking(CustomerID,CompanyName,Color_01,UuDai_TieuDe) VALUES (42,N'Legacy company',N'#123456',N'Legacy title');
INSERT dbo.TCustomer_MyTracking_Banner(MyTrackingID,CustomerID,SortOrder,Url_Hinh,Str4) VALUES (1,42,0,N'https://example.test/old.png',N'Preserve old extra column');
INSERT dbo.TCustomer_NguoiLienLac(CustomerID,Ten,TinhCach,Chuc_Vu,Trang_Thai,Phong_Ban,Create_Date) VALUES (42,N'Legacy contact',N'legacy-user',0,1,0,GETDATE());
'@
            switch($scenario) {
                'missing-import' { Run-Sql $test 'DROP TABLE dbo.Countries;' }
                'duplicate-mytracking' { Run-Sql $test 'INSERT dbo.TCustomer_MyTracking(CustomerID) VALUES (42);' }
                'obsolete-portal' { Run-Sql $test 'CREATE TABLE dbo.MyTrackingCauHinh (CustomerID bigint); INSERT dbo.MyTrackingCauHinh VALUES (42);' }
                'incompatible-portal' { Run-Sql $test 'CREATE TABLE dbo.PhienThuHoi (Ma_Phien varchar(36) NOT NULL PRIMARY KEY,Het_Han date NOT NULL,Ngay_Thu_Hoi datetime2 NOT NULL);' }
                'wrong-primary-key' { Run-Sql $test 'CREATE TABLE dbo.PhienThuHoi (Ma_Phien varchar(36) NOT NULL,Het_Han datetime2 NOT NULL,Ngay_Thu_Hoi datetime2 NOT NULL,CONSTRAINT PK_PhienThuHoi PRIMARY KEY(Het_Han));' }
                'unknown-migration' { Run-Sql $test 'CREATE SCHEMA [identity];'; Run-Sql $test "CREATE TABLE [identity].__EFMigrationsHistory (MigrationId nvarchar(150) PRIMARY KEY,ProductVersion nvarchar(32)); INSERT [identity].__EFMigrationsHistory VALUES (N'20990101000000_FutureMigration',N'99.0.0');" }
            }
            $unchangedSql="SELECT t.name,c.name AS ColumnName,ty.name AS TypeName,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE t.name IN ('MaVanDon_ChiTietHang','MaVanDon_PCS_DIM','TCustomer','USERS','VungXauVungXa','Catalog_HsCode','TContact','Countries') ORDER BY t.name,c.column_id"
            $before=@(Read-Rows $test $unchangedSql) | ConvertTo-Json -Depth 5 -Compress
            if($scenario -eq 'fresh-and-rerun') {
                Run-Sql $test $setupSql
                Assert-Equal 25 (Scalar $test 'SELECT COUNT(*) FROM sys.tables') 'Final table count'
                Assert-Equal 36 (Scalar $test 'SELECT (SELECT COUNT(*) FROM shipments.__EFMigrationsHistory)+(SELECT COUNT(*) FROM ecommerce.__EFMigrationsHistory)+(SELECT COUNT(*) FROM [identity].__EFMigrationsHistory)') 'Migration baseline count'
                Assert-Equal 8 (Scalar $test 'SELECT COUNT(*) FROM dbo.KenhTMDT') 'Channel seeds'
                Assert-Equal 3 (Scalar $test 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0') 'Active foreign keys'
                Assert-Equal 'Legacy company' (Scalar $test 'SELECT CompanyName FROM dbo.TCustomer_MyTracking WHERE CustomerID=42') 'Legacy brand preserved'
                Assert-Equal '#123456' (Scalar $test 'SELECT Color_01 FROM dbo.TCustomer_MyTracking WHERE CustomerID=42') 'Unmapped legacy column preserved'
                Assert-Equal 'Preserve old extra column' (Scalar $test 'SELECT Str4 FROM dbo.TCustomer_MyTracking_Banner WHERE ID=1') 'Banner extra column preserved'
                Run-Sql $test "UPDATE dbo.TCustomer_MyTracking_Banner SET Url_Hinh=REPLICATE(CONVERT(nvarchar(max),N'A'),6000); UPDATE dbo.KenhTMDT SET Ten_Kenh=N'Customer customized name' WHERE ID=1;"
                Run-Sql $test $setupSql
                Assert-Equal 6000 (Scalar $test 'SELECT LEN(Url_Hinh) FROM dbo.TCustomer_MyTracking_Banner WHERE ID=1') 'Long image preserved on rerun'
                Assert-Equal 'Customer customized name' (Scalar $test 'SELECT Ten_Kenh FROM dbo.KenhTMDT WHERE ID=1') 'Custom seed data preserved on rerun'
                Assert-Equal 1 (Scalar $test 'SELECT COUNT(*) FROM dbo.Countries') 'Imported country data preserved'
                Assert-Equal 1 (Scalar $test 'SELECT COUNT(*) FROM dbo.TCustomer_NguoiLienLac') 'Imported contacts preserved'
                $after=@(Read-Rows $test $unchangedSql) | ConvertTo-Json -Depth 5 -Compress
                Assert-Equal $before $after 'Eight unchanged imported schemas'
            } else {
                $failed=$false
                try { Run-Sql $test $setupSql } catch { $failed=$true }
                if(-not $failed) { throw "Expected rejection: $scenario" }
                Assert-Equal 0 (Scalar $test "SELECT COUNT(*) FROM sys.tables WHERE name IN ('MatHangKhachHang','NhomHangHoa','VanDonNguoiTao','KenhTMDT','KetNoiTMDT','DonTMDT','ThongBao','ThongBao_DaDoc','ThongKeSuDung')") 'No partial portal tables after rejection'
                Assert-Equal 0 (Scalar $test "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MaVanDon') AND name=N'VA_Bill'") 'Column additions rolled back'
                if($scenario -eq 'obsolete-portal') { Assert-Equal 1 (Scalar $test 'SELECT COUNT(*) FROM dbo.MyTrackingCauHinh') 'Old portal data preserved' }
            }
            $passed++; "PASS $scenario"
        } finally {
            if($null -ne $test) { $test.Dispose() }
            if($database -notmatch '^vietan_tests_setup_[0-9a-f]{32}$') { throw 'Invalid disposable database name' }
            Run-Sql $master "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];"
        }
    }
    "$passed setup scenarios passed; disposable databases removed."
} finally { $master.Dispose() }
