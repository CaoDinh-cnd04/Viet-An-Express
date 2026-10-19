-- Việt An Express: cấu trúc portal hiện hành — 15/10/2026.
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

-- A. Điều kiện: các bảng được nạp từ hệ thống cũ phải có sẵn.

IF OBJECT_ID(N'dbo.MaVanDon', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.MaVanDon. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.MaVanDon_ChiTietHang', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.MaVanDon_ChiTietHang. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.MaVanDon_PCS_DIM', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.MaVanDon_PCS_DIM. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.TCustomer', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.TCustomer. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.USERS', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.USERS. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.VungXauVungXa', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.VungXauVungXa. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.Catalog_HsCode', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.Catalog_HsCode. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.TCustomer_NguoiLienLac', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.TCustomer_NguoiLienLac. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.TContact', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.TContact. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.TCustomer_MyTracking', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.TCustomer_MyTracking. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.TCustomer_MyTracking_Banner. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'dbo.Countries', N'U') IS NULL
    THROW 51000, N'Thiếu bảng đã nạp dbo.Countries. Nạp bảng cũ trước rồi chạy lại.', 1;

IF OBJECT_ID(N'shipments.OrderDrafts', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước shipments.OrderDrafts. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'shipments.Shipments', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước shipments.Shipments. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.TaiKhoanNhanVien', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.TaiKhoanNhanVien. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.MyTrackingCauHinh', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.MyTrackingCauHinh. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.GopY', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.GopY. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.GopY_HinhAnh', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.GopY_HinhAnh. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.SuCo', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.SuCo. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.SuCo_TraoDoi', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.SuCo_TraoDoi. Dùng migration để bảo toàn dữ liệu trước.', 1;

IF OBJECT_ID(N'dbo.YeuCauSuaNguoiGui', N'U') IS NOT NULL
    THROW 51000, N'Còn bảng portal đời trước dbo.YeuCauSuaNguoiGui. Dùng migration để bảo toàn dữ liệu trước.', 1;

-- B. Bốn bảng hệ thống cũ có thay đổi cấu trúc.

IF COL_LENGTH(N'dbo.MaVanDon', N'ConsigneeIossNo') IS NULL
    EXEC(N'ALTER TABLE [dbo].[MaVanDon] ADD [ConsigneeIossNo] nvarchar(12) NULL');

IF COL_LENGTH(N'dbo.MaVanDon', N'ConsigneeEoriNo') IS NULL
    EXEC(N'ALTER TABLE [dbo].[MaVanDon] ADD [ConsigneeEoriNo] nvarchar(17) NULL');

IF COL_LENGTH(N'dbo.MaVanDon', N'VA_Bill') IS NULL
    EXEC(N'ALTER TABLE [dbo].[MaVanDon] ADD [VA_Bill] nvarchar(30) NULL');

IF COL_LENGTH(N'dbo.TCustomer_NguoiLienLac', N'Quyen') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_NguoiLienLac] ADD [Quyen] nvarchar(1000) NULL');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'Duong_Dan') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Duong_Dan] varchar(60) NULL');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'Da_Xuat_Ban') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Da_Xuat_Ban] bit NOT NULL CONSTRAINT [DF_TCustomer_MyTracking_Da_Xuat_Ban_Portal] DEFAULT (0)');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'Hinh_Nen') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Hinh_Nen] nvarchar(max) NULL');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'Cau_Hinh_Bo_Sung') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Cau_Hinh_Bo_Sung] nvarchar(max) NULL');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'CreateDate') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [CreateDate] datetime NULL CONSTRAINT [DF_TCustomer_MyTracking_CreateDate_Portal] DEFAULT (GETDATE())');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking', N'ModifyDate') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [ModifyDate] datetime NULL');

IF COL_LENGTH(N'dbo.TCustomer_MyTracking_Banner', N'Cau_Hinh_Bo_Sung') IS NULL
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ADD [Cau_Hinh_Bo_Sung] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UuDai_TieuDe' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.UuDai_TieuDe phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UuDai_TieuDe' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [UuDai_TieuDe] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UuDai_NoiDung' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.UuDai_NoiDung phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UuDai_NoiDung' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [UuDai_NoiDung] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CompanyName' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.CompanyName phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CompanyName' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CompanyName] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'Address' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.Address phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'Address' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Address] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'Tel' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.Tel phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'Tel' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Tel] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'Logo' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.Logo phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'Logo' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Logo] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTruyenThong_01' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.CongTruyenThong_01 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTruyenThong_01' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTruyenThong_01] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTruyenThong_02' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.CongTruyenThong_02 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTruyenThong_02' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTruyenThong_02] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTruyenThong_03' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.CongTruyenThong_03 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTruyenThong_03' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTruyenThong_03] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTinNhan_01' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.CongTinNhan_01 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTinNhan_01' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTinNhan_01] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTinNhan_02' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking.CongTinNhan_02 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'CongTinNhan_02' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTinNhan_02] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Str1' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking_Banner.Str1 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Str1' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Str1] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Url_Hinh' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking_Banner.Url_Hinh phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Url_Hinh' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Url_Hinh] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Link' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking_Banner.Link phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Link' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Link] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Str2' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking_Banner.Str2 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Str2' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Str2] nvarchar(max) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Str3' AND system_type_id=231)
    THROW 51000, N'Cột gốc dbo.TCustomer_MyTracking_Banner.Str3 phải có kiểu nvarchar.', 1;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'Str3' AND (max_length<>-1 OR is_nullable=0))
    EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Str3] nvarchar(max) NULL');

-- C. Mười bảng portal hiện hành, khóa, index và danh mục kênh.

-- Module Shipments

IF SCHEMA_ID(N'shipments') IS NULL EXEC(N'CREATE SCHEMA [shipments];');

IF OBJECT_ID(N'shipments.LegacyOrderNumberSequence', N'SO') IS NULL
    EXEC(N'CREATE SEQUENCE [shipments].[LegacyOrderNumberSequence] START WITH 90000001 INCREMENT BY 1 NO CYCLE;');

IF OBJECT_ID(N'dbo.MatHangKhachHang',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[MatHangKhachHang] (
    [ID] bigint NOT NULL IDENTITY,
    [CustomerID] bigint NOT NULL,
    [Loai] varchar(10) NOT NULL,
    [Khoa] varchar(64) NOT NULL,
    [Yeu_Thich] bit NOT NULL DEFAULT CAST(0 AS bit),
    [Da_Xoa] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
    [ModifyDate] datetime NULL,
    CONSTRAINT [PK_MatHangKhachHang] PRIMARY KEY ([ID])
);');

IF OBJECT_ID(N'dbo.NhomHangHoa',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[NhomHangHoa] (
    [ID] bigint NOT NULL IDENTITY,
    [CustomerID] bigint NULL,
    [Ten_Nhom] nvarchar(150) NOT NULL,
    [Yeu_Thich] bit NOT NULL DEFAULT CAST(0 AS bit),
    [Thu_Tu] int NOT NULL DEFAULT 0,
    [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
    [ModifyDate] datetime NULL,
    CONSTRAINT [PK_NhomHangHoa] PRIMARY KEY ([ID])
);');

IF OBJECT_ID(N'dbo.VanDonNguoiTao',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[VanDonNguoiTao] (
    [MaVanDon_ID] bigint NOT NULL,
    [CustomerID] bigint NOT NULL,
    [StaffID] bigint NOT NULL,
    [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_VanDonNguoiTao] PRIMARY KEY ([MaVanDon_ID])
);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MatHangKhachHang') AND name=N'UX_MatHangKhachHang_CustomerID_Loai_Khoa')
    EXEC(N'CREATE UNIQUE INDEX [UX_MatHangKhachHang_CustomerID_Loai_Khoa] ON [dbo].[MatHangKhachHang] ([CustomerID], [Loai], [Khoa]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.NhomHangHoa') AND name=N'IX_NhomHangHoa_CustomerID_Ten_Nhom')
    EXEC(N'CREATE INDEX [IX_NhomHangHoa_CustomerID_Ten_Nhom] ON [dbo].[NhomHangHoa] ([CustomerID], [Ten_Nhom]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VanDonNguoiTao') AND name=N'IX_VanDonNguoiTao_StaffID')
    EXEC(N'CREATE INDEX [IX_VanDonNguoiTao_StaffID] ON [dbo].[VanDonNguoiTao] ([StaffID], [MaVanDon_ID]);');

-- Module Ecommerce

IF OBJECT_ID(N'dbo.KenhTMDT',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[KenhTMDT] (
    [ID] int NOT NULL,
    [Ma_Kenh] varchar(30) NOT NULL,
    [Ten_Kenh] nvarchar(100) NOT NULL,
    [Kieu_Ket_Noi] varchar(20) NOT NULL,
    [Website] varchar(255) NULL,
    [Dang_Hoat_Dong] bit NOT NULL,
    [Day_Tracking] bit NOT NULL,
    [Thu_Tu] int NOT NULL,
    [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
    [ModifyDate] datetime NULL,
    CONSTRAINT [PK_KenhTMDT] PRIMARY KEY ([ID]),
    CONSTRAINT [AK_KenhTMDT_Ma_Kenh] UNIQUE ([Ma_Kenh])
);');

IF OBJECT_ID(N'dbo.KetNoiTMDT',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[KetNoiTMDT] (
    [ID] bigint NOT NULL IDENTITY,
    [CustomerID] bigint NOT NULL,
    [Ma_Kenh] varchar(30) NOT NULL,
    [Ma_Shop] varchar(255) NOT NULL,
    [Ten_Shop] nvarchar(200) NOT NULL,
    [Ten_Mien_Shop] varchar(255) NULL,
    [Shop_Cipher] varchar(255) NULL,
    [Khu_Vuc] varchar(20) NULL,
    [Tien_Te] char(3) NULL,
    [Quyen_Truy_Cap] varchar(1000) NULL,
    [Access_Token_Ma_Hoa] varchar(max) NULL,
    [Access_Token_Het_Han] datetime NULL,
    [Refresh_Token_Ma_Hoa] varchar(max) NULL,
    [Refresh_Token_Het_Han] datetime NULL,
    [Webhook_IDs] varchar(2000) NULL,
    [Trang_Thai] varchar(20) NOT NULL,
    [Ngay_Ket_Noi] datetime NOT NULL,
    [Dong_Bo_Lan_Cuoi] datetime NULL,
    [Loi_Gan_Nhat] nvarchar(1000) NULL,
    [Ngay_Loi] datetime NULL,
    [Ngay_Ngat_Ket_Noi] datetime NULL,
    [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
    [ModifyDate] datetime NULL,
    CONSTRAINT [PK_KetNoiTMDT] PRIMARY KEY ([ID]),
    CONSTRAINT [CK_KetNoiTMDT_Trang_Thai] CHECK ([Trang_Thai] IN (''active'', ''expired'', ''error'', ''revoked'')),
    CONSTRAINT [FK_KetNoiTMDT_KenhTMDT_Ma_Kenh] FOREIGN KEY ([Ma_Kenh]) REFERENCES [dbo].[KenhTMDT] ([Ma_Kenh]) ON DELETE NO ACTION
);');

IF OBJECT_ID(N'dbo.DonTMDT',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[DonTMDT] (
    [ID] bigint NOT NULL IDENTITY,
    [CustomerID] bigint NOT NULL,
    [KetNoiTMDT_ID] bigint NULL,
    [Nguon] varchar(30) NOT NULL,
    [Ma_Don_San] varchar(100) NULL,
    [So_Don] nvarchar(100) NOT NULL,
    [Bill] varchar(50) NULL,
    [Trang_Thai] varchar(20) NOT NULL,
    [Nguoi_Nhan] nvarchar(200) NULL,
    [Cong_Ty] nvarchar(200) NULL,
    [Dien_Thoai] varchar(50) NULL,
    [Email] varchar(255) NULL,
    [Dia_Chi1] nvarchar(255) NULL,
    [Dia_Chi2] nvarchar(255) NULL,
    [Thanh_Pho] nvarchar(100) NULL,
    [Bang] nvarchar(100) NULL,
    [Ma_Buu_Chinh] varchar(20) NULL,
    [Ma_Nuoc] char(2) NULL,
    [Ten_Nuoc] nvarchar(100) NULL,
    [So_San_Pham] int NOT NULL,
    [Can_Nang] decimal(10,3) NULL,
    [Tien_Te] char(3) NULL,
    [Tong_Tien] decimal(18,2) NULL,
    [San_Pham] nvarchar(max) NULL,
    [Ghi_Chu] nvarchar(1000) NULL,
    [Ngay_Dat_San] datetime NULL,
    [Dich_Vu] nvarchar(50) NULL,
    [Hub] nvarchar(100) NULL,
    [Chi_Nhanh] nvarchar(50) NULL,
    [Hai_Quan] nvarchar(max) NULL,
    [Ngay_Day_Tracking] datetime NULL,
    [Ngay_Sua] datetime NULL,
    [Ngay_Xoa] datetime NULL,
    [Nhan_Vien_Tao] bigint NULL,
    [Ngay_Xac_Nhan] datetime NULL,
    [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
    [ModifyDate] datetime NULL,
    CONSTRAINT [PK_DonTMDT] PRIMARY KEY ([ID]),
    CONSTRAINT [CK_DonTMDT_Trang_Thai] CHECK ([Trang_Thai] IN (''created'', ''picked_up'', ''departed'', ''delivered'', ''exception'', ''weighing'')),
    CONSTRAINT [FK_DonTMDT_KetNoiTMDT_ID] FOREIGN KEY ([KetNoiTMDT_ID]) REFERENCES [dbo].[KetNoiTMDT] ([ID]) ON DELETE NO ACTION
);');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=1 AND Ma_Kenh<>N'shopify') OR (Ma_Kenh=N'shopify' AND ID<>1))
    THROW 51000, N'ID hoặc mã kênh shopify đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=1)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (1, ''oauth2'', ''shopify'', ''2026-10-05T00:00:00.000'', CAST(1 AS bit), NULL, N''Shopify'', 1, CAST(1 AS bit), ''https://www.shopify.com'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=2 AND Ma_Kenh<>N'tiktok') OR (Ma_Kenh=N'tiktok' AND ID<>2))
    THROW 51000, N'ID hoặc mã kênh tiktok đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=2)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (2, ''oauth2'', ''tiktok'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''TikTok Shop'', 2, CAST(1 AS bit), ''https://seller.tiktokshop.com'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=3 AND Ma_Kenh<>N'amazon') OR (Ma_Kenh=N'amazon' AND ID<>3))
    THROW 51000, N'ID hoặc mã kênh amazon đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=3)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (3, ''oauth2'', ''amazon'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Amazon'', 3, CAST(1 AS bit), ''https://sellercentral.amazon.com'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=4 AND Ma_Kenh<>N'ebay') OR (Ma_Kenh=N'ebay' AND ID<>4))
    THROW 51000, N'ID hoặc mã kênh ebay đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=4)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (4, ''oauth2'', ''ebay'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''eBay'', 4, CAST(1 AS bit), ''https://www.ebay.com'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=5 AND Ma_Kenh<>N'etsy') OR (Ma_Kenh=N'etsy' AND ID<>5))
    THROW 51000, N'ID hoặc mã kênh etsy đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=5)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (5, ''oauth2'', ''etsy'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Etsy'', 5, CAST(1 AS bit), ''https://www.etsy.com'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=6 AND Ma_Kenh<>N'woocommerce') OR (Ma_Kenh=N'woocommerce' AND ID<>6))
    THROW 51000, N'ID hoặc mã kênh woocommerce đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=6)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (6, ''api_key'', ''woocommerce'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''WooCommerce'', 6, CAST(1 AS bit), ''https://woocommerce.com'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=7 AND Ma_Kenh<>N'shopee') OR (Ma_Kenh=N'shopee' AND ID<>7))
    THROW 51000, N'ID hoặc mã kênh shopee đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=7)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (7, ''oauth2'', ''shopee'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Shopee'', 7, CAST(0 AS bit), ''https://banhang.shopee.vn'');');

IF EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE (ID=8 AND Ma_Kenh<>N'lazada') OR (Ma_Kenh=N'lazada' AND ID<>8))
    THROW 51000, N'ID hoặc mã kênh lazada đã được dùng khác danh mục trong code; cần đối chiếu.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.KenhTMDT WHERE ID=8)
    EXEC(N'INSERT INTO dbo.KenhTMDT ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website]) VALUES (8, ''oauth2'', ''lazada'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Lazada'', 8, CAST(0 AS bit), ''https://sellercenter.lazada.vn'');');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'IX_DonTMDT_CustomerID_Ngay_Dat_San')
    EXEC(N'CREATE INDEX [IX_DonTMDT_CustomerID_Ngay_Dat_San] ON [dbo].[DonTMDT] ([CustomerID], [Ngay_Dat_San]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'IX_DonTMDT_CustomerID_Nhan_Vien_Tao')
    EXEC(N'CREATE INDEX [IX_DonTMDT_CustomerID_Nhan_Vien_Tao] ON [dbo].[DonTMDT] ([CustomerID], [Nhan_Vien_Tao]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'IX_DonTMDT_KetNoiTMDT_ID')
    EXEC(N'CREATE INDEX [IX_DonTMDT_KetNoiTMDT_ID] ON [dbo].[DonTMDT] ([KetNoiTMDT_ID]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'UX_DonTMDT_CustomerID_Nguon_Ma_Don_San')
    EXEC(N'CREATE UNIQUE INDEX [UX_DonTMDT_CustomerID_Nguon_Ma_Don_San] ON [dbo].[DonTMDT] ([CustomerID], [Nguon], [Ma_Don_San]) WHERE [Ma_Don_San] IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND name=N'IX_KetNoiTMDT_Ma_Kenh_Ma_Shop')
    EXEC(N'CREATE INDEX [IX_KetNoiTMDT_Ma_Kenh_Ma_Shop] ON [dbo].[KetNoiTMDT] ([Ma_Kenh], [Ma_Shop]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND name=N'UX_KetNoiTMDT_CustomerID_Ma_Kenh_Ma_Shop')
    EXEC(N'CREATE UNIQUE INDEX [UX_KetNoiTMDT_CustomerID_Ma_Kenh_Ma_Shop] ON [dbo].[KetNoiTMDT] ([CustomerID], [Ma_Kenh], [Ma_Shop]);');

-- Module Identity

IF OBJECT_ID(N'dbo.PhienThuHoi',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[PhienThuHoi] (
    [Ma_Phien] varchar(36) NOT NULL,
    [Het_Han] datetime2 NOT NULL,
    [Ngay_Thu_Hoi] datetime2 NOT NULL,
    CONSTRAINT [PK_PhienThuHoi] PRIMARY KEY ([Ma_Phien])
);');

IF OBJECT_ID(N'dbo.ThongBao',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[ThongBao] (
    [ID] bigint NOT NULL IDENTITY,
    [CustomerID] bigint NULL,
    [Tieu_De] nvarchar(200) NOT NULL,
    [Noi_Dung] nvarchar(4000) NOT NULL,
    [Duong_Dan] nvarchar(500) NULL,
    [Quan_Trong] bit NOT NULL,
    [Ngay_Tao] datetime NOT NULL,
    [Nguoi_Tao] nvarchar(100) NOT NULL,
    [Email_Trang_Thai] varchar(20) NOT NULL,
    [Email_So_Nguoi_Nhan] int NOT NULL,
    [Email_Da_Gui] int NOT NULL,
    [Email_Loi] nvarchar(500) NULL,
    CONSTRAINT [PK_ThongBao] PRIMARY KEY ([ID])
);');

IF OBJECT_ID(N'dbo.ThongKeSuDung',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[ThongKeSuDung] (
    [Id] uniqueidentifier NOT NULL,
    [Visitor] nvarchar(100) NOT NULL,
    [Audience] nvarchar(12) NOT NULL,
    [Path] nvarchar(80) NOT NULL,
    [Kind] nvarchar(24) NOT NULL,
    [Mode] nvarchar(8) NULL,
    [FlowId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ThongKeSuDung] PRIMARY KEY ([Id])
);');

IF OBJECT_ID(N'dbo.ThongBao_DaDoc',N'U') IS NULL
    EXEC(N'CREATE TABLE [dbo].[ThongBao_DaDoc] (
    [ID] bigint NOT NULL IDENTITY,
    [ThongBao_ID] bigint NOT NULL,
    [CustomerID] bigint NOT NULL,
    [TaiKhoanCon_ID] bigint NOT NULL,
    [Ngay_Doc] datetime NOT NULL,
    CONSTRAINT [PK_ThongBao_DaDoc] PRIMARY KEY ([ID]),
    CONSTRAINT [FK_ThongBao_DaDoc_ThongBao_ThongBao_ID] FOREIGN KEY ([ThongBao_ID]) REFERENCES [dbo].[ThongBao] ([ID]) ON DELETE CASCADE
);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PhienThuHoi') AND name=N'IX_PhienThuHoi_Het_Han')
    EXEC(N'CREATE INDEX [IX_PhienThuHoi_Het_Han] ON [dbo].[PhienThuHoi] ([Het_Han]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThongBao') AND name=N'IX_ThongBao_Ngay_Tao')
    EXEC(N'CREATE INDEX [IX_ThongBao_Ngay_Tao] ON [dbo].[ThongBao] ([Ngay_Tao]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThongBao_DaDoc') AND name=N'IX_ThongBao_DaDoc_ThongBao_ID')
    EXEC(N'CREATE INDEX [IX_ThongBao_DaDoc_ThongBao_ID] ON [dbo].[ThongBao_DaDoc] ([ThongBao_ID]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThongBao_DaDoc') AND name=N'UX_ThongBao_DaDoc_TaiKhoan')
    EXEC(N'CREATE UNIQUE INDEX [UX_ThongBao_DaDoc_TaiKhoan] ON [dbo].[ThongBao_DaDoc] ([CustomerID], [TaiKhoanCon_ID], [ThongBao_ID]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThongKeSuDung') AND name=N'IX_ThongKeSuDung_CreatedAt_Audience_Kind')
    EXEC(N'CREATE INDEX [IX_ThongKeSuDung_CreatedAt_Audience_Kind] ON [dbo].[ThongKeSuDung] ([CreatedAt], [Audience], [Kind]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThongKeSuDung') AND name=N'IX_ThongKeSuDung_FlowId_Mode_Kind')
    EXEC(N'CREATE INDEX [IX_ThongKeSuDung_FlowId_Mode_Kind] ON [dbo].[ThongKeSuDung] ([FlowId], [Mode], [Kind]);');

-- D. Index trên hai bảng MyTracking cũ; dừng nếu dữ liệu trùng.

IF EXISTS (SELECT CustomerID FROM dbo.TCustomer_MyTracking GROUP BY CustomerID HAVING COUNT_BIG(*)>1)
    THROW 51000, N'MyTracking có nhiều trang cho một khách; không tự ghi đè hoặc gộp.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UX_TCustomer_MyTracking_CustomerID')
    EXEC(N'CREATE UNIQUE INDEX [UX_TCustomer_MyTracking_CustomerID] ON dbo.[TCustomer_MyTracking] ([CustomerID]);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking') AND name=N'UX_TCustomer_MyTracking_Duong_Dan')
    EXEC(N'CREATE UNIQUE INDEX [UX_TCustomer_MyTracking_Duong_Dan] ON dbo.[TCustomer_MyTracking] ([Duong_Dan]) WHERE [Duong_Dan] IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner') AND name=N'IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder')
    EXEC(N'CREATE INDEX [IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder] ON dbo.[TCustomer_MyTracking_Banner] ([MyTrackingID],[SortOrder]);');

-- E. Kiểm tra cấu trúc trước khi ghi baseline migration; không chỉ bỏ qua bảng đã có.

DECLARE @expectedColumns TABLE (TableName sysname,ColumnName sysname,TypeName sysname,MaxBytes smallint NULL,PrecisionValue tinyint NULL,ScaleValue tinyint NULL,Nullable bit,IdentityValue bit);

INSERT @expectedColumns VALUES
(N'MaVanDon',N'ConsigneeIossNo',N'nvarchar',24,NULL,NULL,1,0),
(N'MaVanDon',N'ConsigneeEoriNo',N'nvarchar',34,NULL,NULL,1,0),
(N'MaVanDon',N'VA_Bill',N'nvarchar',60,NULL,NULL,1,0),
(N'TCustomer_NguoiLienLac',N'Quyen',N'nvarchar',2000,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'Duong_Dan',N'varchar',60,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'Da_Xuat_Ban',N'bit',NULL,NULL,NULL,0,0),
(N'TCustomer_MyTracking',N'Hinh_Nen',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'Cau_Hinh_Bo_Sung',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CreateDate',N'datetime',NULL,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'ModifyDate',N'datetime',NULL,NULL,NULL,1,0),
(N'TCustomer_MyTracking_Banner',N'Cau_Hinh_Bo_Sung',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'UuDai_TieuDe',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'UuDai_NoiDung',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CompanyName',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'Address',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'Tel',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'Logo',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CongTruyenThong_01',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CongTruyenThong_02',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CongTruyenThong_03',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CongTinNhan_01',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking',N'CongTinNhan_02',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking_Banner',N'Str1',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking_Banner',N'Url_Hinh',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking_Banner',N'Link',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking_Banner',N'Str2',N'nvarchar',-1,NULL,NULL,1,0),
(N'TCustomer_MyTracking_Banner',N'Str3',N'nvarchar',-1,NULL,NULL,1,0),
(N'MatHangKhachHang',N'ID',N'bigint',NULL,NULL,NULL,0,1),
(N'MatHangKhachHang',N'CustomerID',N'bigint',NULL,NULL,NULL,0,0),
(N'MatHangKhachHang',N'Loai',N'varchar',10,NULL,NULL,0,0),
(N'MatHangKhachHang',N'Khoa',N'varchar',64,NULL,NULL,0,0),
(N'MatHangKhachHang',N'Yeu_Thich',N'bit',NULL,NULL,NULL,0,0),
(N'MatHangKhachHang',N'Da_Xoa',N'bit',NULL,NULL,NULL,0,0),
(N'MatHangKhachHang',N'CreateDate',N'datetime',NULL,NULL,NULL,0,0),
(N'MatHangKhachHang',N'ModifyDate',N'datetime',NULL,NULL,NULL,1,0),
(N'NhomHangHoa',N'ID',N'bigint',NULL,NULL,NULL,0,1),
(N'NhomHangHoa',N'CustomerID',N'bigint',NULL,NULL,NULL,1,0),
(N'NhomHangHoa',N'Ten_Nhom',N'nvarchar',300,NULL,NULL,0,0),
(N'NhomHangHoa',N'Yeu_Thich',N'bit',NULL,NULL,NULL,0,0),
(N'NhomHangHoa',N'Thu_Tu',N'int',NULL,NULL,NULL,0,0),
(N'NhomHangHoa',N'CreateDate',N'datetime',NULL,NULL,NULL,0,0),
(N'NhomHangHoa',N'ModifyDate',N'datetime',NULL,NULL,NULL,1,0),
(N'VanDonNguoiTao',N'MaVanDon_ID',N'bigint',NULL,NULL,NULL,0,0),
(N'VanDonNguoiTao',N'CustomerID',N'bigint',NULL,NULL,NULL,0,0),
(N'VanDonNguoiTao',N'StaffID',N'bigint',NULL,NULL,NULL,0,0),
(N'VanDonNguoiTao',N'CreateDate',N'datetime',NULL,NULL,NULL,0,0),
(N'KenhTMDT',N'ID',N'int',NULL,NULL,NULL,0,0),
(N'KenhTMDT',N'Ma_Kenh',N'varchar',30,NULL,NULL,0,0),
(N'KenhTMDT',N'Ten_Kenh',N'nvarchar',200,NULL,NULL,0,0),
(N'KenhTMDT',N'Kieu_Ket_Noi',N'varchar',20,NULL,NULL,0,0),
(N'KenhTMDT',N'Website',N'varchar',255,NULL,NULL,1,0),
(N'KenhTMDT',N'Dang_Hoat_Dong',N'bit',NULL,NULL,NULL,0,0),
(N'KenhTMDT',N'Day_Tracking',N'bit',NULL,NULL,NULL,0,0),
(N'KenhTMDT',N'Thu_Tu',N'int',NULL,NULL,NULL,0,0),
(N'KenhTMDT',N'CreateDate',N'datetime',NULL,NULL,NULL,0,0),
(N'KenhTMDT',N'ModifyDate',N'datetime',NULL,NULL,NULL,1,0),
(N'KetNoiTMDT',N'ID',N'bigint',NULL,NULL,NULL,0,1),
(N'KetNoiTMDT',N'CustomerID',N'bigint',NULL,NULL,NULL,0,0),
(N'KetNoiTMDT',N'Ma_Kenh',N'varchar',30,NULL,NULL,0,0),
(N'KetNoiTMDT',N'Ma_Shop',N'varchar',255,NULL,NULL,0,0),
(N'KetNoiTMDT',N'Ten_Shop',N'nvarchar',400,NULL,NULL,0,0),
(N'KetNoiTMDT',N'Ten_Mien_Shop',N'varchar',255,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Shop_Cipher',N'varchar',255,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Khu_Vuc',N'varchar',20,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Tien_Te',N'char',3,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Quyen_Truy_Cap',N'varchar',1000,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Access_Token_Ma_Hoa',N'varchar',-1,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Access_Token_Het_Han',N'datetime',NULL,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Refresh_Token_Ma_Hoa',N'varchar',-1,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Refresh_Token_Het_Han',N'datetime',NULL,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Webhook_IDs',N'varchar',2000,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Trang_Thai',N'varchar',20,NULL,NULL,0,0),
(N'KetNoiTMDT',N'Ngay_Ket_Noi',N'datetime',NULL,NULL,NULL,0,0),
(N'KetNoiTMDT',N'Dong_Bo_Lan_Cuoi',N'datetime',NULL,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Loi_Gan_Nhat',N'nvarchar',2000,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Ngay_Loi',N'datetime',NULL,NULL,NULL,1,0),
(N'KetNoiTMDT',N'Ngay_Ngat_Ket_Noi',N'datetime',NULL,NULL,NULL,1,0),
(N'KetNoiTMDT',N'CreateDate',N'datetime',NULL,NULL,NULL,0,0),
(N'KetNoiTMDT',N'ModifyDate',N'datetime',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'ID',N'bigint',NULL,NULL,NULL,0,1),
(N'DonTMDT',N'CustomerID',N'bigint',NULL,NULL,NULL,0,0),
(N'DonTMDT',N'KetNoiTMDT_ID',N'bigint',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'Nguon',N'varchar',30,NULL,NULL,0,0),
(N'DonTMDT',N'Ma_Don_San',N'varchar',100,NULL,NULL,1,0),
(N'DonTMDT',N'So_Don',N'nvarchar',200,NULL,NULL,0,0),
(N'DonTMDT',N'Bill',N'varchar',50,NULL,NULL,1,0),
(N'DonTMDT',N'Trang_Thai',N'varchar',20,NULL,NULL,0,0),
(N'DonTMDT',N'Nguoi_Nhan',N'nvarchar',400,NULL,NULL,1,0),
(N'DonTMDT',N'Cong_Ty',N'nvarchar',400,NULL,NULL,1,0),
(N'DonTMDT',N'Dien_Thoai',N'varchar',50,NULL,NULL,1,0),
(N'DonTMDT',N'Email',N'varchar',255,NULL,NULL,1,0),
(N'DonTMDT',N'Dia_Chi1',N'nvarchar',510,NULL,NULL,1,0),
(N'DonTMDT',N'Dia_Chi2',N'nvarchar',510,NULL,NULL,1,0),
(N'DonTMDT',N'Thanh_Pho',N'nvarchar',200,NULL,NULL,1,0),
(N'DonTMDT',N'Bang',N'nvarchar',200,NULL,NULL,1,0),
(N'DonTMDT',N'Ma_Buu_Chinh',N'varchar',20,NULL,NULL,1,0),
(N'DonTMDT',N'Ma_Nuoc',N'char',2,NULL,NULL,1,0),
(N'DonTMDT',N'Ten_Nuoc',N'nvarchar',200,NULL,NULL,1,0),
(N'DonTMDT',N'So_San_Pham',N'int',NULL,NULL,NULL,0,0),
(N'DonTMDT',N'Can_Nang',N'decimal',NULL,10,3,1,0),
(N'DonTMDT',N'Tien_Te',N'char',3,NULL,NULL,1,0),
(N'DonTMDT',N'Tong_Tien',N'decimal',NULL,18,2,1,0),
(N'DonTMDT',N'San_Pham',N'nvarchar',-1,NULL,NULL,1,0),
(N'DonTMDT',N'Ghi_Chu',N'nvarchar',2000,NULL,NULL,1,0),
(N'DonTMDT',N'Ngay_Dat_San',N'datetime',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'Dich_Vu',N'nvarchar',100,NULL,NULL,1,0),
(N'DonTMDT',N'Hub',N'nvarchar',200,NULL,NULL,1,0),
(N'DonTMDT',N'Chi_Nhanh',N'nvarchar',100,NULL,NULL,1,0),
(N'DonTMDT',N'Hai_Quan',N'nvarchar',-1,NULL,NULL,1,0),
(N'DonTMDT',N'Ngay_Day_Tracking',N'datetime',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'Ngay_Sua',N'datetime',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'Ngay_Xoa',N'datetime',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'Nhan_Vien_Tao',N'bigint',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'Ngay_Xac_Nhan',N'datetime',NULL,NULL,NULL,1,0),
(N'DonTMDT',N'CreateDate',N'datetime',NULL,NULL,NULL,0,0),
(N'DonTMDT',N'ModifyDate',N'datetime',NULL,NULL,NULL,1,0),
(N'PhienThuHoi',N'Ma_Phien',N'varchar',36,NULL,NULL,0,0),
(N'PhienThuHoi',N'Het_Han',N'datetime2',NULL,NULL,NULL,0,0),
(N'PhienThuHoi',N'Ngay_Thu_Hoi',N'datetime2',NULL,NULL,NULL,0,0),
(N'ThongBao',N'ID',N'bigint',NULL,NULL,NULL,0,1),
(N'ThongBao',N'CustomerID',N'bigint',NULL,NULL,NULL,1,0),
(N'ThongBao',N'Tieu_De',N'nvarchar',400,NULL,NULL,0,0),
(N'ThongBao',N'Noi_Dung',N'nvarchar',8000,NULL,NULL,0,0),
(N'ThongBao',N'Duong_Dan',N'nvarchar',1000,NULL,NULL,1,0),
(N'ThongBao',N'Quan_Trong',N'bit',NULL,NULL,NULL,0,0),
(N'ThongBao',N'Ngay_Tao',N'datetime',NULL,NULL,NULL,0,0),
(N'ThongBao',N'Nguoi_Tao',N'nvarchar',200,NULL,NULL,0,0),
(N'ThongBao',N'Email_Trang_Thai',N'varchar',20,NULL,NULL,0,0),
(N'ThongBao',N'Email_So_Nguoi_Nhan',N'int',NULL,NULL,NULL,0,0),
(N'ThongBao',N'Email_Da_Gui',N'int',NULL,NULL,NULL,0,0),
(N'ThongBao',N'Email_Loi',N'nvarchar',1000,NULL,NULL,1,0),
(N'ThongKeSuDung',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0,0),
(N'ThongKeSuDung',N'Visitor',N'nvarchar',200,NULL,NULL,0,0),
(N'ThongKeSuDung',N'Audience',N'nvarchar',24,NULL,NULL,0,0),
(N'ThongKeSuDung',N'Path',N'nvarchar',160,NULL,NULL,0,0),
(N'ThongKeSuDung',N'Kind',N'nvarchar',48,NULL,NULL,0,0),
(N'ThongKeSuDung',N'Mode',N'nvarchar',16,NULL,NULL,1,0),
(N'ThongKeSuDung',N'FlowId',N'uniqueidentifier',NULL,NULL,NULL,1,0),
(N'ThongKeSuDung',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0,0),
(N'ThongBao_DaDoc',N'ID',N'bigint',NULL,NULL,NULL,0,1),
(N'ThongBao_DaDoc',N'ThongBao_ID',N'bigint',NULL,NULL,NULL,0,0),
(N'ThongBao_DaDoc',N'CustomerID',N'bigint',NULL,NULL,NULL,0,0),
(N'ThongBao_DaDoc',N'TaiKhoanCon_ID',N'bigint',NULL,NULL,NULL,0,0),
(N'ThongBao_DaDoc',N'Ngay_Doc',N'datetime',NULL,NULL,NULL,0,0);

IF EXISTS (
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
END;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.MatHangKhachHang') AND name=N'PK_MatHangKhachHang' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_MatHangKhachHang trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.NhomHangHoa') AND name=N'PK_NhomHangHoa' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_NhomHangHoa trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.VanDonNguoiTao') AND name=N'PK_VanDonNguoiTao' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_VanDonNguoiTao trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.KenhTMDT') AND name=N'PK_KenhTMDT' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_KenhTMDT trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.KenhTMDT') AND name=N'AK_KenhTMDT_Ma_Kenh' AND type=N'UQ')
    THROW 51000, N'Thiếu ràng buộc AK_KenhTMDT_Ma_Kenh trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND name=N'PK_KetNoiTMDT' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_KetNoiTMDT trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND name=N'CK_KetNoiTMDT_Trang_Thai' AND type=N'C')
    THROW 51000, N'Thiếu ràng buộc CK_KetNoiTMDT_Trang_Thai trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND name=N'CK_KetNoiTMDT_Trang_Thai' AND (is_disabled=1 OR is_not_trusted=1))
    THROW 51000, N'Ràng buộc CK_KetNoiTMDT_Trang_Thai đang tắt hoặc chưa trusted.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND name=N'FK_KetNoiTMDT_KenhTMDT_Ma_Kenh' AND type=N'F')
    THROW 51000, N'Thiếu ràng buộc FK_KetNoiTMDT_KenhTMDT_Ma_Kenh trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'PK_DonTMDT' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_DonTMDT trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'CK_DonTMDT_Trang_Thai' AND type=N'C')
    THROW 51000, N'Thiếu ràng buộc CK_DonTMDT_Trang_Thai trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'CK_DonTMDT_Trang_Thai' AND (is_disabled=1 OR is_not_trusted=1))
    THROW 51000, N'Ràng buộc CK_DonTMDT_Trang_Thai đang tắt hoặc chưa trusted.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.DonTMDT') AND name=N'FK_DonTMDT_KetNoiTMDT_ID' AND type=N'F')
    THROW 51000, N'Thiếu ràng buộc FK_DonTMDT_KetNoiTMDT_ID trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.PhienThuHoi') AND name=N'PK_PhienThuHoi' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_PhienThuHoi trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ThongBao') AND name=N'PK_ThongBao' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_ThongBao trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ThongKeSuDung') AND name=N'PK_ThongKeSuDung' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_ThongKeSuDung trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ThongBao_DaDoc') AND name=N'PK_ThongBao_DaDoc' AND type=N'PK')
    THROW 51000, N'Thiếu ràng buộc PK_ThongBao_DaDoc trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ThongBao_DaDoc') AND name=N'FK_ThongBao_DaDoc_ThongBao_ThongBao_ID' AND type=N'F')
    THROW 51000, N'Thiếu ràng buộc FK_ThongBao_DaDoc_ThongBao_ThongBao_ID trong bảng có sẵn; cần đối chiếu cấu trúc.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id WHERE fk.parent_object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND fk.name=N'FK_KetNoiTMDT_KenhTMDT_Ma_Kenh' AND fk.referenced_object_id=OBJECT_ID(N'dbo.KenhTMDT') AND COL_NAME(fc.parent_object_id,fc.parent_column_id)=N'Ma_Kenh' AND COL_NAME(fc.referenced_object_id,fc.referenced_column_id)=N'Ma_Kenh' AND fk.delete_referential_action=0 AND fk.is_disabled=0 AND fk.is_not_trusted=0 AND (SELECT COUNT(*) FROM sys.foreign_key_columns WHERE constraint_object_id=fk.object_id)=1)
    THROW 51000, N'Khóa ngoại FK_KetNoiTMDT_KenhTMDT_Ma_Kenh không đúng định nghĩa hoặc chưa bật/trusted.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id WHERE fk.parent_object_id=OBJECT_ID(N'dbo.DonTMDT') AND fk.name=N'FK_DonTMDT_KetNoiTMDT_ID' AND fk.referenced_object_id=OBJECT_ID(N'dbo.KetNoiTMDT') AND COL_NAME(fc.parent_object_id,fc.parent_column_id)=N'KetNoiTMDT_ID' AND COL_NAME(fc.referenced_object_id,fc.referenced_column_id)=N'ID' AND fk.delete_referential_action=0 AND fk.is_disabled=0 AND fk.is_not_trusted=0 AND (SELECT COUNT(*) FROM sys.foreign_key_columns WHERE constraint_object_id=fk.object_id)=1)
    THROW 51000, N'Khóa ngoại FK_DonTMDT_KetNoiTMDT_ID không đúng định nghĩa hoặc chưa bật/trusted.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id WHERE fk.parent_object_id=OBJECT_ID(N'dbo.ThongBao_DaDoc') AND fk.name=N'FK_ThongBao_DaDoc_ThongBao_ThongBao_ID' AND fk.referenced_object_id=OBJECT_ID(N'dbo.ThongBao') AND COL_NAME(fc.parent_object_id,fc.parent_column_id)=N'ThongBao_ID' AND COL_NAME(fc.referenced_object_id,fc.referenced_column_id)=N'ID' AND fk.delete_referential_action=1 AND fk.is_disabled=0 AND fk.is_not_trusted=0 AND (SELECT COUNT(*) FROM sys.foreign_key_columns WHERE constraint_object_id=fk.object_id)=1)
    THROW 51000, N'Khóa ngoại FK_ThongBao_DaDoc_ThongBao_ThongBao_ID không đúng định nghĩa hoặc chưa bật/trusted.', 1;

DECLARE @expectedIndexes TABLE (TableName sysname,IndexName sysname,UniqueValue bit,KeyColumns nvarchar(1000),FilterSql nvarchar(1000));

INSERT @expectedIndexes VALUES
(N'MatHangKhachHang',N'PK_MatHangKhachHang',1,N'ID',N''),
(N'NhomHangHoa',N'PK_NhomHangHoa',1,N'ID',N''),
(N'VanDonNguoiTao',N'PK_VanDonNguoiTao',1,N'MaVanDon_ID',N''),
(N'MatHangKhachHang',N'UX_MatHangKhachHang_CustomerID_Loai_Khoa',1,N'CustomerID,Loai,Khoa',N''),
(N'NhomHangHoa',N'IX_NhomHangHoa_CustomerID_Ten_Nhom',0,N'CustomerID,Ten_Nhom',N''),
(N'VanDonNguoiTao',N'IX_VanDonNguoiTao_StaffID',0,N'StaffID,MaVanDon_ID',N''),
(N'KenhTMDT',N'PK_KenhTMDT',1,N'ID',N''),
(N'KenhTMDT',N'AK_KenhTMDT_Ma_Kenh',1,N'Ma_Kenh',N''),
(N'KetNoiTMDT',N'PK_KetNoiTMDT',1,N'ID',N''),
(N'DonTMDT',N'PK_DonTMDT',1,N'ID',N''),
(N'DonTMDT',N'IX_DonTMDT_CustomerID_Ngay_Dat_San',0,N'CustomerID,Ngay_Dat_San',N''),
(N'DonTMDT',N'IX_DonTMDT_CustomerID_Nhan_Vien_Tao',0,N'CustomerID,Nhan_Vien_Tao',N''),
(N'DonTMDT',N'IX_DonTMDT_KetNoiTMDT_ID',0,N'KetNoiTMDT_ID',N''),
(N'DonTMDT',N'UX_DonTMDT_CustomerID_Nguon_Ma_Don_San',1,N'CustomerID,Nguon,Ma_Don_San',N'[Ma_Don_San] IS NOT NULL'),
(N'KetNoiTMDT',N'IX_KetNoiTMDT_Ma_Kenh_Ma_Shop',0,N'Ma_Kenh,Ma_Shop',N''),
(N'KetNoiTMDT',N'UX_KetNoiTMDT_CustomerID_Ma_Kenh_Ma_Shop',1,N'CustomerID,Ma_Kenh,Ma_Shop',N''),
(N'PhienThuHoi',N'PK_PhienThuHoi',1,N'Ma_Phien',N''),
(N'ThongBao',N'PK_ThongBao',1,N'ID',N''),
(N'ThongKeSuDung',N'PK_ThongKeSuDung',1,N'Id',N''),
(N'ThongBao_DaDoc',N'PK_ThongBao_DaDoc',1,N'ID',N''),
(N'PhienThuHoi',N'IX_PhienThuHoi_Het_Han',0,N'Het_Han',N''),
(N'ThongBao',N'IX_ThongBao_Ngay_Tao',0,N'Ngay_Tao',N''),
(N'ThongBao_DaDoc',N'IX_ThongBao_DaDoc_ThongBao_ID',0,N'ThongBao_ID',N''),
(N'ThongBao_DaDoc',N'UX_ThongBao_DaDoc_TaiKhoan',1,N'CustomerID,TaiKhoanCon_ID,ThongBao_ID',N''),
(N'ThongKeSuDung',N'IX_ThongKeSuDung_CreatedAt_Audience_Kind',0,N'CreatedAt,Audience,Kind',N''),
(N'ThongKeSuDung',N'IX_ThongKeSuDung_FlowId_Mode_Kind',0,N'FlowId,Mode,Kind',N''),
(N'TCustomer_MyTracking',N'UX_TCustomer_MyTracking_CustomerID',1,N'CustomerID',N''),
(N'TCustomer_MyTracking',N'UX_TCustomer_MyTracking_Duong_Dan',1,N'Duong_Dan',N'[Duong_Dan] IS NOT NULL'),
(N'TCustomer_MyTracking_Banner',N'IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder',0,N'MyTrackingID,SortOrder',N'');

IF EXISTS (
    SELECT 1 FROM @expectedIndexes e
    LEFT JOIN sys.indexes i ON i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.TableName)) AND i.name=e.IndexName
    WHERE i.index_id IS NULL OR i.is_unique<>e.UniqueValue OR i.is_disabled=1 OR i.is_hypothetical=1
       OR e.KeyColumns<>STUFF((SELECT N','+c.name FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 ORDER BY ic.key_ordinal FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,N'')
       OR UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(i.filter_definition,N''),N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''))<>UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(e.FilterSql,N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''))
)
    THROW 51000, N'Index có sẵn khác định nghĩa trong code hoặc bị vô hiệu hóa; chưa ghi baseline.', 1;

-- F. Lịch sử EF: ghi baseline của schema đã kiểm tra, không chạy lại các bước tạo/xóa bảng đời trước.

IF SCHEMA_ID(N'shipments') IS NULL EXEC(N'CREATE SCHEMA [shipments]');

IF OBJECT_ID(N'shipments.__EFMigrationsHistory',N'U') IS NULL
    EXEC(N'CREATE TABLE [shipments].[__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));');

EXEC(N'IF EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId NOT IN (N''20260929030747_InitialCreate'',N''20260929065200_AddOrderDraftsAndLegacyNumbers'',N''20260929070409_AddDraftPrintedOrderNumber'',N''20260929123938_MapLegacyOrderLines'',N''20260929132942_DropShipmentsKeepDrafts'',N''20260930064849_AddGoodsCategories'',N''20261001081556_AddCatalogMarks'',N''20261002021735_AddReceiverEuTaxNumbers'',N''20261006022517_AddOrderCreators'',N''20261009063942_AddVaBillCode'',N''20261009070046_MapRemoteAreaColumns'',N''20261014094748_MapHsCodeCatalog'',N''20261015092126_DropOrderDraftsUseMaVanDon'')) THROW 51000, N''Lịch sử Shipments chứa migration không có trong bản code này; không hạ phiên bản.'', 1;');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20260929030747_InitialCreate'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20260929030747_InitialCreate'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20260929065200_AddOrderDraftsAndLegacyNumbers'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20260929065200_AddOrderDraftsAndLegacyNumbers'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20260929070409_AddDraftPrintedOrderNumber'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20260929070409_AddDraftPrintedOrderNumber'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20260929123938_MapLegacyOrderLines'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20260929123938_MapLegacyOrderLines'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20260929132942_DropShipmentsKeepDrafts'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20260929132942_DropShipmentsKeepDrafts'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20260930064849_AddGoodsCategories'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20260930064849_AddGoodsCategories'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261001081556_AddCatalogMarks'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261001081556_AddCatalogMarks'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261002021735_AddReceiverEuTaxNumbers'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261002021735_AddReceiverEuTaxNumbers'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261006022517_AddOrderCreators'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261006022517_AddOrderCreators'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261009063942_AddVaBillCode'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261009063942_AddVaBillCode'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261009070046_MapRemoteAreaColumns'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261009070046_MapRemoteAreaColumns'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261014094748_MapHsCodeCatalog'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261014094748_MapHsCodeCatalog'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId=N''20261015092126_DropOrderDraftsUseMaVanDon'') INSERT [shipments].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261015092126_DropOrderDraftsUseMaVanDon'',N''10.0.12'');');

IF SCHEMA_ID(N'ecommerce') IS NULL EXEC(N'CREATE SCHEMA [ecommerce]');

IF OBJECT_ID(N'ecommerce.__EFMigrationsHistory',N'U') IS NULL
    EXEC(N'CREATE TABLE [ecommerce].[__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));');

EXEC(N'IF EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId NOT IN (N''20261005062005_AddEcommerceChannels'',N''20261005075915_AddMarketplaceOrders'',N''20261005081053_AddManualOrderFields'',N''20261005085112_AddOrderEditedAt'',N''20261005091255_AddOrderDeletedAt'',N''20261005094336_AddOrderConfirmedAt'',N''20261006084009_AddOrderCreatedByStaff'')) THROW 51000, N''Lịch sử Ecommerce chứa migration không có trong bản code này; không hạ phiên bản.'', 1;');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261005062005_AddEcommerceChannels'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261005062005_AddEcommerceChannels'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261005075915_AddMarketplaceOrders'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261005075915_AddMarketplaceOrders'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261005081053_AddManualOrderFields'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261005081053_AddManualOrderFields'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261005085112_AddOrderEditedAt'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261005085112_AddOrderEditedAt'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261005091255_AddOrderDeletedAt'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261005091255_AddOrderDeletedAt'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261005094336_AddOrderConfirmedAt'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261005094336_AddOrderConfirmedAt'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [ecommerce].[__EFMigrationsHistory] WHERE MigrationId=N''20261006084009_AddOrderCreatedByStaff'') INSERT [ecommerce].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261006084009_AddOrderCreatedByStaff'',N''10.0.12'');');

IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity]');

IF OBJECT_ID(N'identity.__EFMigrationsHistory',N'U') IS NULL
    EXEC(N'CREATE TABLE [identity].[__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));');

EXEC(N'IF EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId NOT IN (N''20261006014226_AddStaffAccountsAndMyTracking'',N''20261009090958_AddFeedback'',N''20261009092814_AddFeedbackRating'',N''20261009100000_HideFeedbackFromCustomer'',N''20261012023956_AddFeedbackSurvey'',N''20261012030236_AddNotifications'',N''20261012031211_AddNotificationLink'',N''20261012034406_AddTroubles'',N''20261012065017_AddRevokedSessions'',N''20261013024726_AddUsageAnalytics'',N''20261013091550_AddSenderInfoRequests'',N''20261013094827_EnsureSenderReviewConsistency'',N''20261015084334_MergeSupportIntoSuCo'',N''20261015090242_MoveStaffToNguoiLienLac'',N''20261015093129_MoveMyTrackingToLegacyTables'',N''20261015093943_MoveSupportToTContact'')) THROW 51000, N''Lịch sử Identity chứa migration không có trong bản code này; không hạ phiên bản.'', 1;');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261006014226_AddStaffAccountsAndMyTracking'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261006014226_AddStaffAccountsAndMyTracking'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261009090958_AddFeedback'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261009090958_AddFeedback'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261009092814_AddFeedbackRating'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261009092814_AddFeedbackRating'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261009100000_HideFeedbackFromCustomer'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261009100000_HideFeedbackFromCustomer'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261012023956_AddFeedbackSurvey'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261012023956_AddFeedbackSurvey'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261012030236_AddNotifications'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261012030236_AddNotifications'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261012031211_AddNotificationLink'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261012031211_AddNotificationLink'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261012034406_AddTroubles'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261012034406_AddTroubles'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261012065017_AddRevokedSessions'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261012065017_AddRevokedSessions'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261013024726_AddUsageAnalytics'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261013024726_AddUsageAnalytics'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261013091550_AddSenderInfoRequests'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261013091550_AddSenderInfoRequests'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261013094827_EnsureSenderReviewConsistency'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261013094827_EnsureSenderReviewConsistency'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261015084334_MergeSupportIntoSuCo'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261015084334_MergeSupportIntoSuCo'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261015090242_MoveStaffToNguoiLienLac'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261015090242_MoveStaffToNguoiLienLac'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261015093129_MoveMyTrackingToLegacyTables'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261015093129_MoveMyTrackingToLegacyTables'',N''10.0.12'');');

EXEC(N'IF NOT EXISTS (SELECT 1 FROM [identity].[__EFMigrationsHistory] WHERE MigrationId=N''20261015093943_MoveSupportToTContact'') INSERT [identity].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N''20261015093943_MoveSupportToTContact'',N''10.0.12'');');

COMMIT TRANSACTION;
    PRINT N'Đã hoàn tất schema portal hiện hành. Các bảng đã nạp không thay đổi cấu trúc được giữ nguyên.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
