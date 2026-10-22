-- Việt An Express — cập nhật 20/10/2026: invoice riêng từng kiện (Chuyên tuyến Úc / EU / Canada).
-- Chọn đúng database vietanexpress trong SSMS rồi chạy TOÀN BỘ file (F5). Chạy lại được nhiều lần.
--
-- Thêm cột dbo.MaVanDon_ChiTietHang.Kien (int, cho phép NULL): số thứ tự kiện của dòng hàng; NULL = invoice chung như cũ.
-- Không đổi dữ liệu có sẵn; phần mềm cũ / CMS không đọc cột này nên không bị ảnh hưởng.
-- Chạy file này TRƯỚC khi đẩy bản backend mới (bản mới đọc / ghi cột Kien).

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.MaVanDon_ChiTietHang', N'Kien') IS NULL
        EXEC(N'ALTER TABLE [dbo].[MaVanDon_ChiTietHang] ADD [Kien] int NULL');

    -- Ghi lịch sử migration để backend (update-database.ps1) không thêm cột lần nữa.
    IF OBJECT_ID(N'shipments.__EFMigrationsHistory', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM [shipments].[__EFMigrationsHistory] WHERE MigrationId = N'20261020091106_AddInvoiceLinePackage')
        INSERT [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261020091106_AddInvoiceLinePackage', N'10.0.12');

    COMMIT TRANSACTION;
    PRINT N'Đã thêm cột MaVanDon_ChiTietHang.Kien.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
