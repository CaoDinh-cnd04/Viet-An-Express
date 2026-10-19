-- =====================================================================
-- Tạo toàn bộ bảng / cột portal Việt An Express thêm vào database.
-- Sinh tự động từ migration EF Core (dotnet ef migrations script --idempotent)
-- của 3 module: Shipments, Ecommerce, Identity. Ngày sinh: 15/10/2026.
--
-- Chạy được nhiều lần: migration nào đã áp (có trong <schema>.__EFMigrationsHistory) sẽ bỏ qua.
-- Yêu cầu: đã nạp trước các bảng hệ thống cũ (script không tạo các bảng này):
--   dbo.MaVanDon, dbo.MaVanDon_ChiTietHang, dbo.MaVanDon_PCS_DIM,
--   dbo.TCustomer, dbo.USERS, dbo.VungXauVungXa, dbo.Catalog_HsCode,
--   dbo.TCustomer_NguoiLienLac, dbo.TContact, dbo.TCustomer_MyTracking, dbo.TCustomer_MyTracking_Banner (legacy-tables.sql) —
--   script thêm cột cần thiết và chuyển tài khoản con, Hỗ trợ & sự cố, MyTracking vào các bảng này.
-- Sao lưu database trước khi chạy.
-- =====================================================================

--   dbo.TCustomer_MyTracking + dbo.TCustomer_MyTracking_Banner (legacy-tables.sql).
USE vietanexpress;
GO


-- =====================================================================
-- Module Shipments
-- =====================================================================

IF OBJECT_ID(N'[shipments].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'shipments') IS NULL EXEC(N'CREATE SCHEMA [shipments];');
    CREATE TABLE [shipments].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'shipments') IS NULL EXEC(N'CREATE SCHEMA [shipments];');
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE SEQUENCE [shipments].[ShipmentCodeSequence] START WITH 10000001 INCREMENT BY 1 NO CYCLE;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE TABLE [shipments].[Shipments] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [CustomerReference] nvarchar(100) NULL,
        [Status] nvarchar(20) NOT NULL,
        [ContentType] nvarchar(20) NOT NULL,
        [ServiceCode] nvarchar(30) NOT NULL,
        [GoodsDescription] nvarchar(500) NOT NULL,
        [TotalPieces] int NOT NULL,
        [ActualWeightKg] decimal(10,2) NOT NULL,
        [VolumetricWeightKg] decimal(10,2) NOT NULL,
        [ChargeableWeightKg] decimal(10,2) NOT NULL,
        [BillIssuedAt] datetimeoffset NULL,
        [DispatchedAt] datetimeoffset NULL,
        [DeliveredAt] datetimeoffset NULL,
        [ReceivedBy] nvarchar(150) NULL,
        [LastFailureReason] nvarchar(500) NULL,
        [CancelledAt] datetimeoffset NULL,
        [CancelReason] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        [DeclaredValue_Amount] decimal(18,2) NOT NULL,
        [DeclaredValue_Currency] nchar(3) NOT NULL,
        [Receiver_City] nvarchar(100) NOT NULL,
        [Receiver_CompanyName] nvarchar(250) NULL,
        [Receiver_ContactName] nvarchar(150) NOT NULL,
        [Receiver_CountryCode] nchar(2) NOT NULL,
        [Receiver_Email] nvarchar(150) NULL,
        [Receiver_Line1] nvarchar(250) NOT NULL,
        [Receiver_Line2] nvarchar(250) NULL,
        [Receiver_Phone] nvarchar(50) NOT NULL,
        [Receiver_PostalCode] nvarchar(20) NULL,
        [Receiver_State] nvarchar(100) NULL,
        [Sender_City] nvarchar(100) NOT NULL,
        [Sender_CompanyName] nvarchar(250) NULL,
        [Sender_ContactName] nvarchar(150) NOT NULL,
        [Sender_CountryCode] nchar(2) NOT NULL,
        [Sender_Email] nvarchar(150) NULL,
        [Sender_Line1] nvarchar(250) NOT NULL,
        [Sender_Line2] nvarchar(250) NULL,
        [Sender_Phone] nvarchar(50) NOT NULL,
        [Sender_PostalCode] nvarchar(20) NULL,
        [Sender_State] nvarchar(100) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        [BranchId] uniqueidentifier NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE TABLE [shipments].[ShipmentPackages] (
        [Id] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [WeightKg] decimal(10,2) NOT NULL,
        [LengthCm] decimal(10,2) NOT NULL,
        [WidthCm] decimal(10,2) NOT NULL,
        [HeightCm] decimal(10,2) NOT NULL,
        [ShipmentId] uniqueidentifier NULL,
        CONSTRAINT [PK_ShipmentPackages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShipmentPackages_Shipments_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [shipments].[Shipments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE TABLE [shipments].[ShipmentTrackingEvents] (
        [Id] uniqueidentifier NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Location] nvarchar(200) NULL,
        [IsPublic] bit NOT NULL,
        [ShipmentId] uniqueidentifier NULL,
        CONSTRAINT [PK_ShipmentTrackingEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShipmentTrackingEvents_Shipments_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [shipments].[Shipments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShipmentPackages_ShipmentId] ON [shipments].[ShipmentPackages] ([ShipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_BranchId] ON [shipments].[Shipments] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Shipments_Code] ON [shipments].[Shipments] ([Code]) WHERE [Code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_CreatedAt] ON [shipments].[Shipments] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_CustomerId] ON [shipments].[Shipments] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_Status] ON [shipments].[Shipments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShipmentTrackingEvents_ShipmentId_OccurredAt] ON [shipments].[ShipmentTrackingEvents] ([ShipmentId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929030747_InitialCreate'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929030747_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929065200_AddOrderDraftsAndLegacyNumbers'
)
BEGIN
    CREATE SEQUENCE [shipments].[LegacyOrderNumberSequence] START WITH 90000001 INCREMENT BY 1 NO CYCLE;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929065200_AddOrderDraftsAndLegacyNumbers'
)
BEGIN
    CREATE TABLE [shipments].[OrderDrafts] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NULL,
        [Status] nvarchar(10) NOT NULL,
        [Consignee] nvarchar(250) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [ServiceName] nvarchar(100) NOT NULL,
        [Branch] nvarchar(50) NOT NULL,
        [Reference] nvarchar(100) NOT NULL,
        [PiecesText] nvarchar(100) NOT NULL,
        [Content] nvarchar(250) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        [BranchId] uniqueidentifier NULL,
        CONSTRAINT [PK_OrderDrafts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929065200_AddOrderDraftsAndLegacyNumbers'
)
BEGIN
    CREATE INDEX [IX_OrderDrafts_BranchId] ON [shipments].[OrderDrafts] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929065200_AddOrderDraftsAndLegacyNumbers'
)
BEGIN
    CREATE INDEX [IX_OrderDrafts_CreatedBy] ON [shipments].[OrderDrafts] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929065200_AddOrderDraftsAndLegacyNumbers'
)
BEGIN
    CREATE INDEX [IX_OrderDrafts_CustomerId] ON [shipments].[OrderDrafts] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929065200_AddOrderDraftsAndLegacyNumbers'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929065200_AddOrderDraftsAndLegacyNumbers', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070409_AddDraftPrintedOrderNumber'
)
BEGIN
    ALTER TABLE [shipments].[OrderDrafts] ADD [PrintedOrderNumber] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070409_AddDraftPrintedOrderNumber'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrderDrafts_PrintedOrderNumber] ON [shipments].[OrderDrafts] ([PrintedOrderNumber]) WHERE [PrintedOrderNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070409_AddDraftPrintedOrderNumber'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929070409_AddDraftPrintedOrderNumber', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123938_MapLegacyOrderLines'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929123938_MapLegacyOrderLines', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DROP TABLE [shipments].[ShipmentPackages];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DROP TABLE [shipments].[ShipmentTrackingEvents];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DROP TABLE [shipments].[Shipments];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DROP INDEX [IX_OrderDrafts_CreatedBy] ON [shipments].[OrderDrafts];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DROP SEQUENCE [shipments].[ShipmentCodeSequence];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DROP INDEX [IX_OrderDrafts_CustomerId] ON [shipments].[OrderDrafts];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    ALTER TABLE [shipments].[OrderDrafts] ADD [LegacyCustomerId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    IF OBJECT_ID(N'customers.Customers') IS NOT NULL
        EXEC(N'UPDATE d SET LegacyCustomerId = c.LegacyId
               FROM shipments.OrderDrafts d JOIN customers.Customers c ON c.Id = d.CustomerId');
    DELETE FROM shipments.OrderDrafts WHERE LegacyCustomerId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[shipments].[OrderDrafts]') AND [c].[name] = N'CustomerId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [shipments].[OrderDrafts] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [shipments].[OrderDrafts] DROP COLUMN [CustomerId];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    EXEC sp_rename N'[shipments].[OrderDrafts].[LegacyCustomerId]', N'CustomerId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[shipments].[OrderDrafts]') AND [c].[name] = N'CustomerId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [shipments].[OrderDrafts] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [shipments].[OrderDrafts] ALTER COLUMN [CustomerId] bigint NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    CREATE INDEX [IX_OrderDrafts_CustomerId] ON [shipments].[OrderDrafts] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929132942_DropShipmentsKeepDrafts'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929132942_DropShipmentsKeepDrafts', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930064849_AddGoodsCategories'
)
BEGIN
    CREATE TABLE [dbo].[NhomHangHoa] (
        [ID] bigint NOT NULL IDENTITY,
        [CustomerID] bigint NULL,
        [Ten_Nhom] nvarchar(150) NOT NULL,
        [Yeu_Thich] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Thu_Tu] int NOT NULL DEFAULT 0,
        [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
        [ModifyDate] datetime NULL,
        CONSTRAINT [PK_NhomHangHoa] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930064849_AddGoodsCategories'
)
BEGIN
    CREATE INDEX [IX_NhomHangHoa_CustomerID_Ten_Nhom] ON [dbo].[NhomHangHoa] ([CustomerID], [Ten_Nhom]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930064849_AddGoodsCategories'
)
BEGIN
    INSERT INTO dbo.NhomHangHoa (CustomerID, Ten_Nhom, Yeu_Thich, Thu_Tu) VALUES
    (NULL, N'Quần áo, giày dép', 0, 1),
    (NULL, N'Túi xách, phụ kiện thời trang', 0, 2),
    (NULL, N'Trang sức, đồng hồ', 0, 3),
    (NULL, N'Mỹ phẩm, hóa mỹ phẩm', 0, 4),
    (NULL, N'Thực phẩm khô, đặc sản', 0, 5),
    (NULL, N'Thực phẩm chức năng', 0, 6),
    (NULL, N'Thuốc, dược phẩm', 0, 7),
    (NULL, N'Đồ điện tử', 0, 8),
    (NULL, N'Linh kiện, phụ tùng máy móc', 0, 9),
    (NULL, N'Hàng có pin', 0, 10),
    (NULL, N'Chất lỏng, dầu, nước hoa', 0, 11),
    (NULL, N'Sách, văn phòng phẩm', 0, 12),
    (NULL, N'Đồ gia dụng, nhà bếp', 0, 13),
    (NULL, N'Đồ chơi', 0, 14),
    (NULL, N'Hàng thủ công mỹ nghệ', 0, 15),
    (NULL, N'Vải, nguyên phụ liệu may', 0, 16),
    (NULL, N'Hàng mẫu (sample)', 0, 17),
    (NULL, N'Quà tặng cá nhân', 0, 18);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930064849_AddGoodsCategories'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930064849_AddGoodsCategories', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001081556_AddCatalogMarks'
)
BEGIN
    CREATE TABLE [dbo].[MatHangKhachHang] (
        [ID] bigint NOT NULL IDENTITY,
        [CustomerID] bigint NOT NULL,
        [Loai] varchar(10) NOT NULL,
        [Khoa] varchar(64) NOT NULL,
        [Yeu_Thich] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Da_Xoa] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
        [ModifyDate] datetime NULL,
        CONSTRAINT [PK_MatHangKhachHang] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001081556_AddCatalogMarks'
)
BEGIN
    CREATE UNIQUE INDEX [UX_MatHangKhachHang_CustomerID_Loai_Khoa] ON [dbo].[MatHangKhachHang] ([CustomerID], [Loai], [Khoa]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001081556_AddCatalogMarks'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001081556_AddCatalogMarks', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002021735_AddReceiverEuTaxNumbers'
)
BEGIN
    ALTER TABLE [dbo].[MaVanDon] ADD [ConsigneeIossNo] nvarchar(12) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002021735_AddReceiverEuTaxNumbers'
)
BEGIN
    ALTER TABLE [dbo].[MaVanDon] ADD [ConsigneeEoriNo] nvarchar(17) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002021735_AddReceiverEuTaxNumbers'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002021735_AddReceiverEuTaxNumbers', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006022517_AddOrderCreators'
)
BEGIN
    ALTER TABLE [shipments].[OrderDrafts] ADD [CreatedByStaffId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006022517_AddOrderCreators'
)
BEGIN
    CREATE TABLE [dbo].[VanDonNguoiTao] (
        [MaVanDon_ID] bigint NOT NULL,
        [CustomerID] bigint NOT NULL,
        [StaffID] bigint NOT NULL,
        [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_VanDonNguoiTao] PRIMARY KEY ([MaVanDon_ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006022517_AddOrderCreators'
)
BEGIN
    CREATE INDEX [IX_VanDonNguoiTao_StaffID] ON [dbo].[VanDonNguoiTao] ([StaffID], [MaVanDon_ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006022517_AddOrderCreators'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006022517_AddOrderCreators', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063942_AddVaBillCode'
)
BEGIN
    ALTER TABLE [dbo].[MaVanDon] ADD [VA_Bill] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009063942_AddVaBillCode'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009063942_AddVaBillCode', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009070046_MapRemoteAreaColumns'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009070046_MapRemoteAreaColumns', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014094748_MapHsCodeCatalog'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014094748_MapHsCodeCatalog', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015092126_DropOrderDraftsUseMaVanDon'
)
BEGIN
    DROP TABLE [shipments].[OrderDrafts];
END;

IF NOT EXISTS (
    SELECT * FROM [shipments].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015092126_DropOrderDraftsUseMaVanDon'
)
BEGIN
    INSERT INTO [shipments].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015092126_DropOrderDraftsUseMaVanDon', N'10.0.12');
END;

COMMIT;
GO


-- =====================================================================
-- Module Ecommerce
-- =====================================================================

IF OBJECT_ID(N'[ecommerce].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'ecommerce') IS NULL EXEC(N'CREATE SCHEMA [ecommerce];');
    CREATE TABLE [ecommerce].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005062005_AddEcommerceChannels'
)
BEGIN
    CREATE TABLE [dbo].[KenhTMDT] (
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
    );
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005062005_AddEcommerceChannels'
)
BEGIN
    CREATE TABLE [dbo].[KetNoiTMDT] (
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
        CONSTRAINT [CK_KetNoiTMDT_Trang_Thai] CHECK ([Trang_Thai] IN ('active', 'expired', 'error', 'revoked')),
        CONSTRAINT [FK_KetNoiTMDT_KenhTMDT_Ma_Kenh] FOREIGN KEY ([Ma_Kenh]) REFERENCES [dbo].[KenhTMDT] ([Ma_Kenh]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005062005_AddEcommerceChannels'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ID', N'Kieu_Ket_Noi', N'Ma_Kenh', N'CreateDate', N'Dang_Hoat_Dong', N'ModifyDate', N'Ten_Kenh', N'Thu_Tu', N'Day_Tracking', N'Website') AND [object_id] = OBJECT_ID(N'[dbo].[KenhTMDT]'))
        SET IDENTITY_INSERT [dbo].[KenhTMDT] ON;
    EXEC(N'INSERT INTO [dbo].[KenhTMDT] ([ID], [Kieu_Ket_Noi], [Ma_Kenh], [CreateDate], [Dang_Hoat_Dong], [ModifyDate], [Ten_Kenh], [Thu_Tu], [Day_Tracking], [Website])
    VALUES (1, ''oauth2'', ''shopify'', ''2026-10-05T00:00:00.000'', CAST(1 AS bit), NULL, N''Shopify'', 1, CAST(1 AS bit), ''https://www.shopify.com''),
    (2, ''oauth2'', ''tiktok'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''TikTok Shop'', 2, CAST(1 AS bit), ''https://seller.tiktokshop.com''),
    (3, ''oauth2'', ''amazon'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Amazon'', 3, CAST(1 AS bit), ''https://sellercentral.amazon.com''),
    (4, ''oauth2'', ''ebay'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''eBay'', 4, CAST(1 AS bit), ''https://www.ebay.com''),
    (5, ''oauth2'', ''etsy'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Etsy'', 5, CAST(1 AS bit), ''https://www.etsy.com''),
    (6, ''api_key'', ''woocommerce'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''WooCommerce'', 6, CAST(1 AS bit), ''https://woocommerce.com''),
    (7, ''oauth2'', ''shopee'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Shopee'', 7, CAST(0 AS bit), ''https://banhang.shopee.vn''),
    (8, ''oauth2'', ''lazada'', ''2026-10-05T00:00:00.000'', CAST(0 AS bit), NULL, N''Lazada'', 8, CAST(0 AS bit), ''https://sellercenter.lazada.vn'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ID', N'Kieu_Ket_Noi', N'Ma_Kenh', N'CreateDate', N'Dang_Hoat_Dong', N'ModifyDate', N'Ten_Kenh', N'Thu_Tu', N'Day_Tracking', N'Website') AND [object_id] = OBJECT_ID(N'[dbo].[KenhTMDT]'))
        SET IDENTITY_INSERT [dbo].[KenhTMDT] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005062005_AddEcommerceChannels'
)
BEGIN
    CREATE INDEX [IX_KetNoiTMDT_Ma_Kenh_Ma_Shop] ON [dbo].[KetNoiTMDT] ([Ma_Kenh], [Ma_Shop]);
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005062005_AddEcommerceChannels'
)
BEGIN
    CREATE UNIQUE INDEX [UX_KetNoiTMDT_CustomerID_Ma_Kenh_Ma_Shop] ON [dbo].[KetNoiTMDT] ([CustomerID], [Ma_Kenh], [Ma_Shop]);
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005062005_AddEcommerceChannels'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005062005_AddEcommerceChannels', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075915_AddMarketplaceOrders'
)
BEGIN
    CREATE TABLE [dbo].[DonTMDT] (
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
        [Ngay_Day_Tracking] datetime NULL,
        [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
        [ModifyDate] datetime NULL,
        CONSTRAINT [PK_DonTMDT] PRIMARY KEY ([ID]),
        CONSTRAINT [CK_DonTMDT_Trang_Thai] CHECK ([Trang_Thai] IN ('created', 'picked_up', 'departed', 'delivered', 'exception', 'weighing')),
        CONSTRAINT [FK_DonTMDT_KetNoiTMDT_ID] FOREIGN KEY ([KetNoiTMDT_ID]) REFERENCES [dbo].[KetNoiTMDT] ([ID]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075915_AddMarketplaceOrders'
)
BEGIN
    CREATE INDEX [IX_DonTMDT_CustomerID_Ngay_Dat_San] ON [dbo].[DonTMDT] ([CustomerID], [Ngay_Dat_San]);
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075915_AddMarketplaceOrders'
)
BEGIN
    CREATE INDEX [IX_DonTMDT_KetNoiTMDT_ID] ON [dbo].[DonTMDT] ([KetNoiTMDT_ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075915_AddMarketplaceOrders'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_DonTMDT_CustomerID_Nguon_Ma_Don_San] ON [dbo].[DonTMDT] ([CustomerID], [Nguon], [Ma_Don_San]) WHERE [Ma_Don_San] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075915_AddMarketplaceOrders'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005075915_AddMarketplaceOrders', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005081053_AddManualOrderFields'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Chi_Nhanh] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005081053_AddManualOrderFields'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Dich_Vu] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005081053_AddManualOrderFields'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Hai_Quan] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005081053_AddManualOrderFields'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Hub] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005081053_AddManualOrderFields'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005081053_AddManualOrderFields', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005085112_AddOrderEditedAt'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Ngay_Sua] datetime NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005085112_AddOrderEditedAt'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005085112_AddOrderEditedAt', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005091255_AddOrderDeletedAt'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Ngay_Xoa] datetime NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005091255_AddOrderDeletedAt'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005091255_AddOrderDeletedAt', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005094336_AddOrderConfirmedAt'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Ngay_Xac_Nhan] datetime NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005094336_AddOrderConfirmedAt'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005094336_AddOrderConfirmedAt', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006084009_AddOrderCreatedByStaff'
)
BEGIN
    ALTER TABLE [dbo].[DonTMDT] ADD [Nhan_Vien_Tao] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006084009_AddOrderCreatedByStaff'
)
BEGIN
    CREATE INDEX [IX_DonTMDT_CustomerID_Nhan_Vien_Tao] ON [dbo].[DonTMDT] ([CustomerID], [Nhan_Vien_Tao]);
END;

IF NOT EXISTS (
    SELECT * FROM [ecommerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006084009_AddOrderCreatedByStaff'
)
BEGIN
    INSERT INTO [ecommerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006084009_AddOrderCreatedByStaff', N'10.0.12');
END;

COMMIT;
GO


-- =====================================================================
-- Module Identity
-- =====================================================================

IF OBJECT_ID(N'[identity].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
    CREATE TABLE [identity].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006014226_AddStaffAccountsAndMyTracking'
)
BEGIN
    CREATE TABLE [dbo].[MyTrackingCauHinh] (
        [CustomerID] bigint NOT NULL,
        [Duong_Dan] varchar(60) NOT NULL,
        [Cau_Hinh] nvarchar(max) NOT NULL,
        [Da_Xuat_Ban] bit NOT NULL,
        [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
        [ModifyDate] datetime NULL,
        CONSTRAINT [PK_MyTrackingCauHinh] PRIMARY KEY ([CustomerID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006014226_AddStaffAccountsAndMyTracking'
)
BEGIN
    CREATE TABLE [dbo].[TaiKhoanNhanVien] (
        [ID] bigint NOT NULL IDENTITY,
        [CustomerID] bigint NOT NULL,
        [Ten_Dang_Nhap] varchar(50) NOT NULL,
        [Mat_Khau_Bam] varchar(200) NOT NULL,
        [Ho_Ten] nvarchar(100) NOT NULL,
        [Email] varchar(150) NULL,
        [Dien_Thoai] varchar(30) NULL,
        [Quyen] varchar(1000) NOT NULL,
        [Dang_Hoat_Dong] bit NOT NULL,
        [Dang_Nhap_Lan_Cuoi] datetime NULL,
        [CreateDate] datetime NOT NULL DEFAULT (GETDATE()),
        [ModifyDate] datetime NULL,
        CONSTRAINT [PK_TaiKhoanNhanVien] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006014226_AddStaffAccountsAndMyTracking'
)
BEGIN
    CREATE UNIQUE INDEX [UX_MyTrackingCauHinh_Duong_Dan] ON [dbo].[MyTrackingCauHinh] ([Duong_Dan]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006014226_AddStaffAccountsAndMyTracking'
)
BEGIN
    CREATE INDEX [IX_TaiKhoanNhanVien_CustomerID] ON [dbo].[TaiKhoanNhanVien] ([CustomerID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006014226_AddStaffAccountsAndMyTracking'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TaiKhoanNhanVien_Ten_Dang_Nhap] ON [dbo].[TaiKhoanNhanVien] ([Ten_Dang_Nhap]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006014226_AddStaffAccountsAndMyTracking'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006014226_AddStaffAccountsAndMyTracking', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009090958_AddFeedback'
)
BEGIN
    CREATE TABLE [dbo].[GopY] (
        [ID] bigint NOT NULL IDENTITY,
        [CustomerID] bigint NOT NULL,
        [Ma_Khach] nvarchar(50) NOT NULL,
        [Ten_Cong_Ty] nvarchar(250) NOT NULL,
        [StaffID] bigint NULL,
        [Nguoi_Gui] nvarchar(100) NOT NULL,
        [Noi_Dung] nvarchar(4000) NOT NULL,
        [Lien_He] nvarchar(200) NULL,
        [Da_Xem] bit NOT NULL,
        [Ngay_Gui] datetime NOT NULL,
        [Ngay_Xem] datetime NULL,
        CONSTRAINT [PK_GopY] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009090958_AddFeedback'
)
BEGIN
    CREATE TABLE [dbo].[GopY_HinhAnh] (
        [ID] bigint NOT NULL IDENTITY,
        [GopY_ID] bigint NOT NULL,
        [Ten_File] nvarchar(255) NOT NULL,
        [Loai_File] varchar(50) NOT NULL,
        [Kich_Thuoc] int NOT NULL,
        [Du_Lieu] varbinary(max) NOT NULL,
        CONSTRAINT [PK_GopY_HinhAnh] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_GopY_HinhAnh_GopY_GopY_ID] FOREIGN KEY ([GopY_ID]) REFERENCES [dbo].[GopY] ([ID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009090958_AddFeedback'
)
BEGIN
    CREATE INDEX [IX_GopY_CustomerID_Ngay_Gui] ON [dbo].[GopY] ([CustomerID], [Ngay_Gui]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009090958_AddFeedback'
)
BEGIN
    CREATE INDEX [IX_GopY_HinhAnh_GopY_ID] ON [dbo].[GopY_HinhAnh] ([GopY_ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009090958_AddFeedback'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009090958_AddFeedback', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009092814_AddFeedbackRating'
)
BEGIN
    ALTER TABLE [dbo].[GopY] ADD [So_Sao] tinyint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009092814_AddFeedbackRating'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009092814_AddFeedbackRating', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009100000_HideFeedbackFromCustomer'
)
BEGIN
    ALTER TABLE [dbo].[GopY] ADD [Khach_Da_An] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009100000_HideFeedbackFromCustomer'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009100000_HideFeedbackFromCustomer', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012023956_AddFeedbackSurvey'
)
BEGIN
    ALTER TABLE [dbo].[GopY] ADD [Khao_Sat] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012023956_AddFeedbackSurvey'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012023956_AddFeedbackSurvey', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012030236_AddNotifications'
)
BEGIN
    CREATE TABLE [dbo].[ThongBao] (
        [ID] bigint NOT NULL IDENTITY,
        [Tieu_De] nvarchar(200) NOT NULL,
        [Noi_Dung] nvarchar(4000) NOT NULL,
        [Quan_Trong] bit NOT NULL,
        [Ngay_Tao] datetime NOT NULL,
        [Nguoi_Tao] nvarchar(100) NOT NULL,
        [Email_Trang_Thai] varchar(20) NOT NULL,
        [Email_So_Nguoi_Nhan] int NOT NULL,
        [Email_Da_Gui] int NOT NULL,
        [Email_Loi] nvarchar(500) NULL,
        CONSTRAINT [PK_ThongBao] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012030236_AddNotifications'
)
BEGIN
    CREATE TABLE [dbo].[ThongBao_DaDoc] (
        [ID] bigint NOT NULL IDENTITY,
        [ThongBao_ID] bigint NOT NULL,
        [CustomerID] bigint NOT NULL,
        [TaiKhoanCon_ID] bigint NOT NULL,
        [Ngay_Doc] datetime NOT NULL,
        CONSTRAINT [PK_ThongBao_DaDoc] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_ThongBao_DaDoc_ThongBao_ThongBao_ID] FOREIGN KEY ([ThongBao_ID]) REFERENCES [dbo].[ThongBao] ([ID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012030236_AddNotifications'
)
BEGIN
    CREATE INDEX [IX_ThongBao_Ngay_Tao] ON [dbo].[ThongBao] ([Ngay_Tao]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012030236_AddNotifications'
)
BEGIN
    CREATE INDEX [IX_ThongBao_DaDoc_ThongBao_ID] ON [dbo].[ThongBao_DaDoc] ([ThongBao_ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012030236_AddNotifications'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ThongBao_DaDoc_TaiKhoan] ON [dbo].[ThongBao_DaDoc] ([CustomerID], [TaiKhoanCon_ID], [ThongBao_ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012030236_AddNotifications'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012030236_AddNotifications', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012031211_AddNotificationLink'
)
BEGIN
    ALTER TABLE [dbo].[ThongBao] ADD [Duong_Dan] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012031211_AddNotificationLink'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012031211_AddNotificationLink', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012034406_AddTroubles'
)
BEGIN
    ALTER TABLE [dbo].[ThongBao] ADD [CustomerID] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012034406_AddTroubles'
)
BEGIN
    CREATE TABLE [dbo].[SuCo] (
        [ID] bigint NOT NULL IDENTITY,
        [CustomerID] bigint NOT NULL,
        [Ma_Khach] nvarchar(50) NOT NULL,
        [Ten_Cong_Ty] nvarchar(250) NOT NULL,
        [StaffID] bigint NULL,
        [Nguoi_Gui] nvarchar(100) NOT NULL,
        [Ma_Van_Don] varchar(50) NOT NULL,
        [Nguoi_Nhan] nvarchar(250) NULL,
        [Quoc_Gia] nvarchar(100) NULL,
        [Loai_Su_Co] nvarchar(100) NOT NULL,
        [Muc_Do] varchar(10) NOT NULL,
        [Mo_Ta] nvarchar(4000) NOT NULL,
        [Nguoi_Yeu_Cau] nvarchar(150) NOT NULL,
        [Lien_He] nvarchar(200) NOT NULL,
        [Trang_Thai] varchar(10) NOT NULL,
        [Ngay_Tao] datetime NOT NULL,
        [Ngay_Cap_Nhat] datetime NOT NULL,
        CONSTRAINT [PK_SuCo] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012034406_AddTroubles'
)
BEGIN
    CREATE TABLE [dbo].[SuCo_TraoDoi] (
        [ID] bigint NOT NULL IDENTITY,
        [SuCo_ID] bigint NOT NULL,
        [Tu_Viet_An] bit NOT NULL,
        [Nguoi_Viet] nvarchar(150) NOT NULL,
        [Noi_Dung] nvarchar(4000) NOT NULL,
        [Trang_Thai_Moi] varchar(10) NULL,
        [Ngay_Tao] datetime NOT NULL,
        CONSTRAINT [PK_SuCo_TraoDoi] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_SuCo_TraoDoi_SuCo_SuCo_ID] FOREIGN KEY ([SuCo_ID]) REFERENCES [dbo].[SuCo] ([ID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012034406_AddTroubles'
)
BEGIN
    CREATE INDEX [IX_SuCo_CustomerID_Ngay_Cap_Nhat] ON [dbo].[SuCo] ([CustomerID], [Ngay_Cap_Nhat]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012034406_AddTroubles'
)
BEGIN
    CREATE INDEX [IX_SuCo_TraoDoi_SuCo_ID] ON [dbo].[SuCo_TraoDoi] ([SuCo_ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012034406_AddTroubles'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012034406_AddTroubles', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065017_AddRevokedSessions'
)
BEGIN
    CREATE TABLE [dbo].[PhienThuHoi] (
        [Ma_Phien] varchar(36) NOT NULL,
        [Het_Han] datetime2 NOT NULL,
        [Ngay_Thu_Hoi] datetime2 NOT NULL,
        CONSTRAINT [PK_PhienThuHoi] PRIMARY KEY ([Ma_Phien])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065017_AddRevokedSessions'
)
BEGIN
    CREATE INDEX [IX_PhienThuHoi_Het_Han] ON [dbo].[PhienThuHoi] ([Het_Han]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065017_AddRevokedSessions'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012065017_AddRevokedSessions', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013024726_AddUsageAnalytics'
)
BEGIN
    CREATE TABLE [dbo].[ThongKeSuDung] (
        [Id] uniqueidentifier NOT NULL,
        [Visitor] nvarchar(100) NOT NULL,
        [Audience] nvarchar(12) NOT NULL,
        [Path] nvarchar(80) NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Mode] nvarchar(8) NULL,
        [FlowId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ThongKeSuDung] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013024726_AddUsageAnalytics'
)
BEGIN
    CREATE INDEX [IX_ThongKeSuDung_CreatedAt_Audience_Kind] ON [dbo].[ThongKeSuDung] ([CreatedAt], [Audience], [Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013024726_AddUsageAnalytics'
)
BEGIN
    CREATE INDEX [IX_ThongKeSuDung_FlowId_Mode_Kind] ON [dbo].[ThongKeSuDung] ([FlowId], [Mode], [Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013024726_AddUsageAnalytics'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013024726_AddUsageAnalytics', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013091550_AddSenderInfoRequests'
)
BEGIN
    CREATE TABLE [dbo].[YeuCauSuaNguoiGui] (
        [ID] bigint NOT NULL IDENTITY,
        [CustomerID] bigint NOT NULL,
        [Ma_Khach] nvarchar(150) NOT NULL,
        [Nguoi_Gui] nvarchar(100) NOT NULL,
        [Ten_Cong_Ty_Cu] nvarchar(250) NOT NULL,
        [Nguoi_Lien_He_Cu] nvarchar(150) NULL,
        [Dien_Thoai_Cu] nvarchar(150) NULL,
        [Email_Cu] nvarchar(150) NULL,
        [Dia_Chi_Cu] nvarchar(250) NULL,
        [Ma_So_Thue_Cu] nvarchar(150) NULL,
        [Ten_Cong_Ty_Moi] nvarchar(250) NOT NULL,
        [Nguoi_Lien_He_Moi] nvarchar(150) NULL,
        [Dien_Thoai_Moi] nvarchar(150) NULL,
        [Email_Moi] nvarchar(150) NULL,
        [Dia_Chi_Moi] nvarchar(250) NULL,
        [Ma_So_Thue_Moi] nvarchar(150) NULL,
        [Ghi_Chu] nvarchar(500) NULL,
        [Trang_Thai] varchar(10) NOT NULL,
        [Ly_Do_Tu_Choi] nvarchar(500) NULL,
        [Nguoi_Duyet] nvarchar(100) NULL,
        [Ngay_Tao] datetime NOT NULL,
        [Ngay_Duyet] datetime NULL,
        CONSTRAINT [PK_YeuCauSuaNguoiGui] PRIMARY KEY ([ID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013091550_AddSenderInfoRequests'
)
BEGIN
    CREATE INDEX [IX_YeuCauSuaNguoiGui_CustomerID_Ngay_Tao] ON [dbo].[YeuCauSuaNguoiGui] ([CustomerID], [Ngay_Tao]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013091550_AddSenderInfoRequests'
)
BEGIN
    CREATE INDEX [IX_YeuCauSuaNguoiGui_Trang_Thai_Ngay_Tao] ON [dbo].[YeuCauSuaNguoiGui] ([Trang_Thai], [Ngay_Tao]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013091550_AddSenderInfoRequests'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013091550_AddSenderInfoRequests', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013094827_EnsureSenderReviewConsistency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_YeuCauSuaNguoiGui_Pending_CustomerID] ON [dbo].[YeuCauSuaNguoiGui] ([CustomerID]) WHERE [Trang_Thai] = ''pending''');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013094827_EnsureSenderReviewConsistency'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013094827_EnsureSenderReviewConsistency', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Trang_Thai');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Trang_Thai] varchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Ten_Cong_Ty');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Ten_Cong_Ty] nvarchar(250) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Nguoi_Yeu_Cau');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Nguoi_Yeu_Cau] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Nguoi_Gui');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Nguoi_Gui] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Ngay_Cap_Nhat');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Ngay_Cap_Nhat] datetime NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Muc_Do');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Muc_Do] varchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Mo_Ta');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Mo_Ta] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Ma_Van_Don');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Ma_Van_Don] varchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Ma_Khach');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Ma_Khach] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Loai_Su_Co');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Loai_Su_Co] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'Lien_He');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [Lien_He] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[SuCo]') AND [c].[name] = N'CustomerID');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [dbo].[SuCo] ALTER COLUMN [CustomerID] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [ID_Cha] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [La_Trao_Doi] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Loai] varchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Tep_Du_Lieu] varbinary(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Tep_Kich_Thuoc] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Tep_Loai] varchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Tep_Ten] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Tieu_De] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD [Tu_Viet_An] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    UPDATE dbo.SuCo SET Loai = 'su_co' WHERE La_Trao_Doi = 0 AND Loai IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN

    INSERT INTO dbo.SuCo (La_Trao_Doi, ID_Cha, Tu_Viet_An, Nguoi_Gui, Mo_Ta, Trang_Thai, Ngay_Tao)
    SELECT 1, SuCo_ID, Tu_Viet_An, Nguoi_Viet, Noi_Dung, Trang_Thai_Moi, Ngay_Tao
    FROM dbo.SuCo_TraoDoi
    ORDER BY ID;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    CREATE INDEX [IX_SuCo_ID_Cha] ON [dbo].[SuCo] ([ID_Cha]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] ADD CONSTRAINT [FK_SuCo_SuCo_ID_Cha] FOREIGN KEY ([ID_Cha]) REFERENCES [dbo].[SuCo] ([ID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DROP TABLE [dbo].[GopY_HinhAnh];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DROP TABLE [dbo].[SuCo_TraoDoi];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    DROP TABLE [dbo].[GopY];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015084334_MergeSupportIntoSuCo'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015084334_MergeSupportIntoSuCo', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015090242_MoveStaffToNguoiLienLac'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_NguoiLienLac] ADD [Quyen] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015090242_MoveStaffToNguoiLienLac'
)
BEGIN

    CREATE TABLE #map (OldId bigint NOT NULL PRIMARY KEY, NewId bigint NOT NULL);

    MERGE dbo.TCustomer_NguoiLienLac AS t
    USING dbo.TaiKhoanNhanVien AS s ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT (CustomerID, Ten, Di_Dong, Email, Chuc_Vu, Trang_Thai, Mo_Ta, Phong_Ban, Create_Date, TinhCach, Quyen)
        VALUES (s.CustomerID, s.Ho_Ten, s.Dien_Thoai, s.Email, 0, CASE WHEN s.Dang_Hoat_Dong = 1 THEN 1 ELSE 0 END,
                s.Mat_Khau_Bam, 0, s.CreateDate, s.Ten_Dang_Nhap, s.Quyen)
    OUTPUT s.ID, inserted.ID INTO #map (OldId, NewId);

    IF OBJECT_ID(N'dbo.SuCo', N'U') IS NOT NULL
        EXEC(N'UPDATE x SET StaffID = m.NewId FROM dbo.SuCo x JOIN #map m ON x.StaffID = m.OldId;');
    IF OBJECT_ID(N'dbo.VanDonNguoiTao', N'U') IS NOT NULL
        EXEC(N'UPDATE x SET StaffID = m.NewId FROM dbo.VanDonNguoiTao x JOIN #map m ON x.StaffID = m.OldId;');
    IF OBJECT_ID(N'dbo.DonTMDT', N'U') IS NOT NULL
        EXEC(N'UPDATE x SET Nhan_Vien_Tao = m.NewId FROM dbo.DonTMDT x JOIN #map m ON x.Nhan_Vien_Tao = m.OldId;');
    IF OBJECT_ID(N'dbo.ThongBao_DaDoc', N'U') IS NOT NULL
        EXEC(N'UPDATE x SET TaiKhoanCon_ID = m.NewId FROM dbo.ThongBao_DaDoc x JOIN #map m ON x.TaiKhoanCon_ID = m.OldId;');

    DROP TABLE #map;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015090242_MoveStaffToNguoiLienLac'
)
BEGIN
    DROP TABLE [dbo].[TaiKhoanNhanVien];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015090242_MoveStaffToNguoiLienLac'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015090242_MoveStaffToNguoiLienLac', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Duong_Dan] varchar(60) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Da_Xuat_Ban] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Hinh_Nen] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [Cau_Hinh_Bo_Sung] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [CreateDate] datetime NULL DEFAULT (GETDATE());
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking] ADD [ModifyDate] datetime NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ADD [Cau_Hinh_Bo_Sung] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'UuDai_TieuDe');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [UuDai_TieuDe] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'UuDai_NoiDung');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [UuDai_NoiDung] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'CompanyName');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CompanyName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'Address');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Address] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'Tel');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Tel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'Logo');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [Logo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'CongTruyenThong_01');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTruyenThong_01] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'CongTruyenThong_02');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTruyenThong_02] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'CongTruyenThong_03');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTruyenThong_03] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'CongTinNhan_01');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTinNhan_01] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking]') AND [c].[name] = N'CongTinNhan_02');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking] ALTER COLUMN [CongTinNhan_02] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking_Banner]') AND [c].[name] = N'Str1');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Str1] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking_Banner]') AND [c].[name] = N'Url_Hinh');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Url_Hinh] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking_Banner]') AND [c].[name] = N'Link');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Link] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking_Banner]') AND [c].[name] = N'Str2');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Str2] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[TCustomer_MyTracking_Banner]') AND [c].[name] = N'Str3');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [dbo].[TCustomer_MyTracking_Banner] ALTER COLUMN [Str3] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    INSERT INTO dbo.TCustomer_MyTracking
        (CustomerID, Duong_Dan, Da_Xuat_Ban, CreateDate, ModifyDate, Cau_Hinh_Bo_Sung, [UuDai_TieuDe], [UuDai_NoiDung], [CompanyName], [Address], [Tel], [Logo], [CongTruyenThong_01], [CongTruyenThong_02], [CongTruyenThong_03], [CongTinNhan_01], [CongTinNhan_02], [Hinh_Nen])
    SELECT s.CustomerID, s.Duong_Dan, s.Da_Xuat_Ban, s.CreateDate, s.ModifyDate, s.Cau_Hinh,
        (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$') j WHERE j.[key] = N'title' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$') j WHERE j.[key] = N'description' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'companyName' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'address' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'phone' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'logo' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'facebook' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'instagram' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'x' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'zalo' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$.brand') j WHERE j.[key] = N'whatsapp' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(s.Cau_Hinh, '$') j WHERE j.[key] = N'background' AND j.[type] = 1)
    FROM dbo.MyTrackingCauHinh s;

    INSERT INTO dbo.TCustomer_MyTracking_Banner
        (MyTrackingID, CustomerID, SortOrder, Cau_Hinh_Bo_Sung, [Str1], [Url_Hinh], [Link], [Str2], [Str3])
    SELECT t.ID, s.CustomerID, CONVERT(int, i.[key]), i.[value],
        (SELECT j.[value] FROM OPENJSON(i.[value], '$') j WHERE j.[key] = N'id' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(i.[value], '$') j WHERE j.[key] = N'src' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(i.[value], '$') j WHERE j.[key] = N'linkUrl' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(i.[value], '$') j WHERE j.[key] = N'title' AND j.[type] = 1),
                    (SELECT j.[value] FROM OPENJSON(i.[value], '$') j WHERE j.[key] = N'buttonText' AND j.[type] = 1)
    FROM dbo.MyTrackingCauHinh s
    JOIN dbo.TCustomer_MyTracking t ON t.CustomerID = s.CustomerID
    CROSS APPLY OPENJSON(s.Cau_Hinh) a
    CROSS APPLY OPENJSON(CASE WHEN a.[type] = 4 THEN a.[value] ELSE N'[]' END) i
    WHERE a.[key] = 'images' AND a.[type] = 4;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.title', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[UuDai_TieuDe] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.description', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[UuDai_NoiDung] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.companyName', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[CompanyName] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.address', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[Address] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.phone', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[Tel] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.logo', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[Logo] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.facebook', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[CongTruyenThong_01] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.instagram', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[CongTruyenThong_02] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.x', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[CongTruyenThong_03] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.zalo', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[CongTinNhan_01] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.brand.whatsapp', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[CongTinNhan_02] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE t SET Cau_Hinh_Bo_Sung = JSON_MODIFY(t.Cau_Hinh_Bo_Sung, '$.background', NULL)
    FROM dbo.TCustomer_MyTracking t JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = t.CustomerID
    WHERE t.[Hinh_Nen] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE b SET Cau_Hinh_Bo_Sung = JSON_MODIFY(b.Cau_Hinh_Bo_Sung, '$.id', NULL)
    FROM dbo.TCustomer_MyTracking_Banner b JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = b.CustomerID
    WHERE b.[Str1] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE b SET Cau_Hinh_Bo_Sung = JSON_MODIFY(b.Cau_Hinh_Bo_Sung, '$.src', NULL)
    FROM dbo.TCustomer_MyTracking_Banner b JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = b.CustomerID
    WHERE b.[Url_Hinh] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE b SET Cau_Hinh_Bo_Sung = JSON_MODIFY(b.Cau_Hinh_Bo_Sung, '$.linkUrl', NULL)
    FROM dbo.TCustomer_MyTracking_Banner b JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = b.CustomerID
    WHERE b.[Link] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE b SET Cau_Hinh_Bo_Sung = JSON_MODIFY(b.Cau_Hinh_Bo_Sung, '$.title', NULL)
    FROM dbo.TCustomer_MyTracking_Banner b JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = b.CustomerID
    WHERE b.[Str2] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    UPDATE b SET Cau_Hinh_Bo_Sung = JSON_MODIFY(b.Cau_Hinh_Bo_Sung, '$.buttonText', NULL)
    FROM dbo.TCustomer_MyTracking_Banner b JOIN dbo.MyTrackingCauHinh s ON s.CustomerID = b.CustomerID
    WHERE b.[Str3] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TCustomer_MyTracking_CustomerID] ON [dbo].[TCustomer_MyTracking] ([CustomerID]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TCustomer_MyTracking_Duong_Dan] ON [dbo].[TCustomer_MyTracking] ([Duong_Dan]) WHERE [Duong_Dan] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    CREATE INDEX [IX_TCustomer_MyTracking_Banner_MyTrackingID_SortOrder] ON [dbo].[TCustomer_MyTracking_Banner] ([MyTrackingID], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    DROP TABLE [dbo].[MyTrackingCauHinh];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093129_MoveMyTrackingToLegacyTables'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015093129_MoveMyTrackingToLegacyTables', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093943_MoveSupportToTContact'
)
BEGIN
    IF OBJECT_ID(N'dbo.TContact', N'U') IS NULL
        THROW 51000, N'Thiếu bảng dbo.TContact. Chạy backend/scripts/legacy-tables.sql trước.', 1;
    INSERT INTO dbo.TContact (Name, Phone, Email, Status, CompanyName, Title, Content, KeyWord, FormType, RegDate, ModifyDate,
        FileID, FileID1, FileID2, FileID3, Des01, Des02, Des03, Des04, Des05, CustID)
    SELECT Nguoi_Yeu_Cau, Lien_He, CASE WHEN Lien_He LIKE N'%_@_%._%' THEN Lien_He END,
        CASE Trang_Thai WHEN 'doing' THEN 1 WHEN 'waitc' THEN 2 WHEN 'done' THEN 3 ELSE 0 END,
        Ten_Cong_Ty, COALESCE(NULLIF(Tieu_De, N''), Loai_Su_Co), Mo_Ta, Loai_Su_Co,
        CASE Loai WHEN 'ho_tro' THEN N'Portal - Hỗ trợ' WHEN 'gop_y' THEN N'Portal - Góp ý' ELSE N'Portal - Sự cố' END,
        Ngay_Tao, ISNULL(Ngay_Cap_Nhat, Ngay_Tao), 0, 0, 0, 0,
        Ma_Van_Don, Muc_Do, Nguoi_Nhan, Quoc_Gia, CONVERT(nvarchar(30), StaffID), CustomerID
    FROM dbo.SuCo
    WHERE La_Trao_Doi = 0
    ORDER BY ID;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093943_MoveSupportToTContact'
)
BEGIN
    ALTER TABLE [dbo].[SuCo] DROP CONSTRAINT [FK_SuCo_SuCo_ID_Cha];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093943_MoveSupportToTContact'
)
BEGIN
    DROP TABLE [dbo].[SuCo];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093943_MoveSupportToTContact'
)
BEGIN
    DROP TABLE [dbo].[YeuCauSuaNguoiGui];
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015093943_MoveSupportToTContact'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015093943_MoveSupportToTContact', N'10.0.12');
END;

COMMIT;
GO

