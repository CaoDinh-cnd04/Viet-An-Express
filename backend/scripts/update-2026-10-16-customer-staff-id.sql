-- Việt An Express — cập nhật 16/10/2026: người tạo vận đơn chuyển sang cột có sẵn MaVanDon.Customer_Staff_ID.
-- Chọn đúng database vietanexpress trong SSMS rồi chạy TOÀN BỘ file (F5). Chạy lại được nhiều lần.
--
-- Trước: bảng phụ dbo.VanDonNguoiTao (MaVanDon_ID → StaffID) ghi vận đơn do tài khoản con nào tạo.
-- Sau:   MaVanDon.Customer_Staff_ID = dbo.TCustomer_NguoiLienLac.ID của tài khoản con tạo đơn; 0 = tài khoản chính / hệ thống cũ.
-- Chép dữ liệu sang rồi xóa bảng VanDonNguoiTao, ghi lịch sử migration để backend không chạy lại.
-- Chạy file này TRƯỚC khi đẩy bản backend mới (bản mới không còn đọc VanDonNguoiTao).

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.VanDonNguoiTao', N'U') IS NOT NULL
    BEGIN
        EXEC(N'
UPDATE m SET m.Customer_Staff_ID = c.StaffID
FROM dbo.MaVanDon m
JOIN dbo.VanDonNguoiTao c ON c.MaVanDon_ID = m.ID;');
        PRINT N'Đã chép người tạo sang MaVanDon.Customer_Staff_ID: ' + CAST(@@ROWCOUNT AS nvarchar(20)) + N' vận đơn.';

        DROP TABLE dbo.VanDonNguoiTao;
        PRINT N'Đã xóa bảng dbo.VanDonNguoiTao.';
    END
    ELSE
        PRINT N'Không còn bảng dbo.VanDonNguoiTao — bỏ qua bước chép dữ liệu.';

    IF OBJECT_ID(N'shipments.__EFMigrationsHistory', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM shipments.__EFMigrationsHistory WHERE MigrationId = N'20261016080004_MoveCreatorToCustomerStaffId')
        INSERT shipments.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20261016080004_MoveCreatorToCustomerStaffId', N'10.0.12');

    -- Migration không đổi cấu trúc: portal chỉ đọc an toàn các cột Ten / Mo_Ta / TinhCach / Quyen đang NULL của bảng cũ.
    IF OBJECT_ID(N'identity.__EFMigrationsHistory', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM [identity].__EFMigrationsHistory WHERE MigrationId = N'20261016083107_AllowNullLegacyStaffColumns')
        INSERT [identity].__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20261016083107_AllowNullLegacyStaffColumns', N'10.0.12');

    COMMIT TRANSACTION;
    PRINT N'Hoàn tất cập nhật 16/10/2026.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
