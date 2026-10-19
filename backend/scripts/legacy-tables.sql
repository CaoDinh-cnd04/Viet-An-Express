-- =====================================================================
-- Bảng hệ thống cũ (database vietanexpress) người dùng cung cấp ngày 15/10/2026, nạp vào database portal (cũng tên vietanexpress).
-- Chạy được nhiều lần: bảng có rồi thì bỏ qua; dữ liệu Countries chỉ nạp khi bảng còn trống.
-- =====================================================================

USE vietanexpress;
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.TCustomer_NguoiLienLac', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TCustomer_NguoiLienLac](
    	[ID] [bigint] IDENTITY(1,1) NOT NULL,
    	[CustomerID] [bigint] NOT NULL,
    	[Ten] [nvarchar](150) NULL,
    	[Di_Dong] [nvarchar](150) NULL,
    	[Email] [nvarchar](150) NULL,
    	[Chuc_Vu] [int] NOT NULL,
    	[Trang_Thai] [int] NOT NULL,
    	[Mo_Ta] [nvarchar](150) NULL,
    	[Phong_Ban] [int] NOT NULL,
    	[Create_Date] [datetime] NOT NULL,
    	[Di_Dong2] [nvarchar](150) NULL,
    	[Email2] [nvarchar](150) NULL,
    	[TinhCach] [nvarchar](150) NULL,
    	[Gioi_Tinh] [int] NULL,
     CONSTRAINT [PK_TCustomer_Nguoi_Lien_Lac] PRIMARY KEY CLUSTERED 
    (
    	[ID] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    ALTER TABLE [dbo].[TCustomer_NguoiLienLac] ADD  CONSTRAINT [DF_TCustomer_NguoiLienLac_Create_Date]  DEFAULT (getdate()) FOR [Create_Date];
END
GO

IF OBJECT_ID(N'dbo.TContact', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TContact](
    	[ID] [int] IDENTITY(1,1) NOT NULL,
    	[Name] [nvarchar](250) NULL,
    	[Phone] [nvarchar](250) NULL,
    	[Email] [nvarchar](250) NULL,
    	[Address] [nvarchar](250) NULL,
    	[Birthday] [nvarchar](250) NULL,
    	[Gender] [int] NULL,
    	[Status] [int] NULL,
    	[CompanyName] [nvarchar](350) NULL,
    	[Title] [nvarchar](300) NULL,
    	[Content] [ntext] NULL,
    	[KeyWord] [nvarchar](250) NULL,
    	[FormType] [nvarchar](250) NULL,
    	[RegDate] [datetime] NOT NULL,
    	[ModifyDate] [datetime] NOT NULL,
    	[FileID] [bigint] NOT NULL,
    	[FileID1] [bigint] NOT NULL,
    	[FileID2] [bigint] NOT NULL,
    	[FileID3] [bigint] NOT NULL,
    	[Des01] [nvarchar](300) NULL,
    	[Des02] [nvarchar](300) NULL,
    	[Des03] [nvarchar](300) NULL,
    	[Des04] [nvarchar](300) NULL,
    	[Des05] [nvarchar](300) NULL,
    	[CustID] [bigint] NULL,
     CONSTRAINT [PK_TContact] PRIMARY KEY CLUSTERED 
    (
    	[ID] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'dbo.TCustomer_MyTracking', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TCustomer_MyTracking](
    	[ID] [int] IDENTITY(1,1) NOT NULL,
    	[CustomerID] [bigint] NOT NULL,
    	[CongTruyenThong_01] [nvarchar](250) NULL,
    	[CongTruyenThong_02] [nvarchar](250) NULL,
    	[CongTruyenThong_03] [nvarchar](250) NULL,
    	[CongTruyenThong_04] [nvarchar](250) NULL,
    	[CongTruyenThong_05] [nvarchar](250) NULL,
    	[CongTruyenThong_06] [nvarchar](250) NULL,
    	[CongTruyenThong_07] [nvarchar](250) NULL,
    	[CongTruyenThong_08] [nvarchar](250) NULL,
    	[CongTruyenThong_09] [nvarchar](250) NULL,
    	[CongTruyenThong_10] [nvarchar](250) NULL,
    	[CongTruyenThong_11] [nvarchar](250) NULL,
    	[CongTruyenThong_12] [nvarchar](250) NULL,
    	[CongTruyenThong_13] [nvarchar](250) NULL,
    	[CongTruyenThong_14] [nvarchar](250) NULL,
    	[CongTruyenThong_15] [nvarchar](250) NULL,
    	[CongTinNhan_01] [nvarchar](250) NULL,
    	[CongTinNhan_02] [nvarchar](250) NULL,
    	[CongTinNhan_03] [nvarchar](250) NULL,
    	[CongTinNhan_04] [nvarchar](250) NULL,
    	[CongTinNhan_05] [nvarchar](250) NULL,
    	[CongTinNhan_06] [nvarchar](250) NULL,
    	[CongTinNhan_07] [nvarchar](250) NULL,
    	[CongTinNhan_08] [nvarchar](250) NULL,
    	[CongTinNhan_09] [nvarchar](250) NULL,
    	[CongTinNhan_10] [nvarchar](250) NULL,
    	[CongTinNhan_11] [nvarchar](250) NULL,
    	[CongTinNhan_12] [nvarchar](250) NULL,
    	[CongTinNhan_13] [nvarchar](250) NULL,
    	[CongTinNhan_14] [nvarchar](250) NULL,
    	[CongTinNhan_15] [nvarchar](250) NULL,
    	[UuDai_TieuDe] [nvarchar](250) NULL,
    	[UuDai_NoiDung] [nvarchar](250) NULL,
    	[Banner_QC] [ntext] NULL,
    	[CompanyName] [nvarchar](250) NULL,
    	[Address] [nvarchar](250) NULL,
    	[Tel] [nvarchar](250) NULL,
    	[Email] [nvarchar](250) NULL,
    	[Logo] [nvarchar](250) NULL,
    	[Color_01] [nvarchar](250) NULL,
    	[Color_02] [nvarchar](250) NULL,
    	[Website] [nvarchar](250) NULL,
    	[Str1] [nvarchar](250) NULL,
    	[Str2] [nvarchar](250) NULL,
     CONSTRAINT [PK_TCustomer_MyTracking] PRIMARY KEY CLUSTERED 
    (
    	[ID] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'dbo.TCustomer_MyTracking_Banner', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TCustomer_MyTracking_Banner](
    	[ID] [int] IDENTITY(1,1) NOT NULL,
    	[MyTrackingID] [int] NULL,
    	[CustomerID] [bigint] NULL,
    	[SortOrder] [int] NULL,
    	[Link] [nvarchar](250) NULL,
    	[Url_Hinh] [nvarchar](250) NULL,
    	[ID_Hinh] [bigint] NULL,
    	[Str1] [nvarchar](250) NULL,
    	[Str2] [nvarchar](250) NULL,
    	[Str3] [nvarchar](250) NULL,
    	[Str4] [nvarchar](250) NULL,
    	[Str5] [nvarchar](250) NULL,
    	[Str6] [nvarchar](250) NULL,
    	[PhienBan_Tracking] [nvarchar](250) NULL,
    	[TypeFile] [nvarchar](50) NULL,
    	[FileName] [nvarchar](250) NULL,
     CONSTRAINT [PK_TCustomer_MyTracking_Banner] PRIMARY KEY CLUSTERED 
    (
    	[ID] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'dbo.Countries', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Countries](
    	[CountryID] [int] IDENTITY(1,1) NOT NULL,
    	[CountryCode] [nvarchar](50) NULL,
    	[CountryName] [nvarchar](50) NOT NULL,
    	[CountryPhone] [nvarchar](50) NULL,
     CONSTRAINT [PK_Countries] PRIMARY KEY CLUSTERED 
    (
    	[CountryID] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Countries)
BEGIN
    SET IDENTITY_INSERT dbo.Countries ON;
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (1, N'AF', N'Afghanistan', N'93');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (2, N'AL', N'Albania', N'355');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (3, N'DZ', N'Algeria', N'213');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (4, N'AS', N'American Samoa', N'1684');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (5, N'AD', N'Andorra', N'376');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (6, N'AO', N'Angola', N'244');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (7, N'AI', N'Anguilla', N'1264');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (8, N'AQ', N'Antarctica', N'672');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (9, N'AG', N'Antigua and Barbuda', N'1268');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (10, N'AR', N'Argentina', N'54');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (11, N'AM', N'Armenia', N'374');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (12, N'AW', N'Aruba', N'297');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (13, N'AU', N'Australia', N'61');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (14, N'AT', N'Austria', N'43');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (15, N'AZ', N'Azerbaijan', N'994');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (16, N'BS', N'Bahamas', N'1242');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (17, N'BH', N'Bahrain', N'973');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (18, N'BD', N'Bangladesh', N'880');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (19, N'BB', N'Barbados', N'1246');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (20, N'BY', N'Belarus', N'375');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (21, N'BE', N'Belgium', N'32');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (22, N'BZ', N'Belize', N'501');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (23, N'BJ', N'Benin', N'229');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (24, N'BM', N'Bermuda', N'1441');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (25, N'BT', N'Bhutan', N'975');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (26, N'BO', N'Bolivia', N'591');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (27, N'BA', N'Bosnia and Herzegovina', N'387');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (28, N'BW', N'Botswana', N'267');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (29, N'BR', N'Brazil', N'55');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (30, N'IO', N'British Indian Ocean Territory', N'246');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (32, N'BN', N'Brunei', N'673');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (33, N'BG', N'Bulgaria', N'359');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (34, N'BF', N'Burkina Faso', N'226');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (35, N'MM', N'Myanmar', N'95');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (36, N'BI', N'Burundi', N'257');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (37, N'KH', N'Cambodia', N'855');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (38, N'CM', N'Cameroon', N'237');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (39, N'CA', N'Canada', N'1');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (40, N'CV', N'Cape Verde', N'238');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (41, N'KY', N'Cayman Islands', N'1345');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (42, N'CF', N'Central African Republic', N'236');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (43, N'TD', N'Chad', N'235');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (44, N'CL', N'Chile', N'56');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (46, N'CX', N'Christmas Island', N'61');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (47, N'CC', N'Cocos (Keeling) Islands', N'61');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (48, N'CO', N'Colombia', N'57');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (49, N'KM', N'Comoros', N'269');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (50, N'CK', N'Cook Islands', N'682');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (51, N'CR', N'Costa Rica', N'506');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (52, N'HR', N'Croatia', N'385');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (53, N'CU', N'Cuba', N'53');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (54, N'CY', N'Cyprus', N'357');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (55, N'CZ', N'Czech Republic', N'420');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (56, N'CD', N'Congo (Dem. Republic Of)', N'243');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (57, N'DK', N'Denmark', N'45');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (58, N'DJ', N'Djibouti', N'253');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (59, N'DM', N'Dominica', N'1767');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (60, N'DO', N'Dominican Republic', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (61, N'EC', N'Ecuador', N'593');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (62, N'EG', N'Egypt', N'20');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (63, N'SV', N'El Salvador', N'503');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (64, N'GQ', N'Equatorial Guinea', N'240');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (65, N'ER', N'Eritrea', N'291');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (66, N'EE', N'Estonia', N'372');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (67, N'ET', N'Ethiopia', N'251');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (68, N'FK', N'Falkland Islands', N'500');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (69, N'FO', N'Faroe Islands', N'298');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (70, N'FJ', N'Fiji', N'679');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (71, N'FI', N'Finland', N'358');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (72, N'FR', N'France', N'33');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (74, N'GA', N'Gabon', N'241');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (75, N'GM', N'Gambia', N'220');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (76, NULL, N'Gaza Strip', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (77, N'GE', N'Georgia', N'995');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (78, N'DE', N'Germany', N'49');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (79, N'GH', N'Ghana', N'233');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (80, N'GI', N'Gibraltar', N'350');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (81, N'GR', N'Greece', N'30');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (82, N'GL', N'Greenland', N'299');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (83, N'GD', N'Grenada', N'1473');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (84, N'GU', N'Guam', N'1671');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (85, N'GT', N'Guatemala', N'502');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (86, N'GN', N'Guinea Republic', N'224');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (87, N'GW', N'Guinea Bissau', N'245');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (88, N'GY', N'Guyana', N'592');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (89, N'HT', N'Haiti', N'509');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (90, N'VA', N'Holy See (Vatican City)', N'379');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (91, N'HN', N'Honduras', N'504');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (92, N'HK', N'Hong Kong', N'852');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (93, N'HU', N'Hungary', N'36');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (94, N'IS', N'Iceland', N'354');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (95, N'IN', N'India', N'91');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (96, N'ID', N'Indonesia', N'62');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (97, N'IR', N'Iran', N'98');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (98, N'IQ', N'Iraq', N'964');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (99, N'IE', N'Ireland', N'353');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (100, N'IM', N'Isle of Man', N'441624');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (101, N'IL', N'Israel', N'972');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (102, N'IT', N'Italy', N'39');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (103, N'CI', N'Cote D''Ivoire', N'225');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (104, N'JM', N'Jamaica', N'1876');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (105, N'JP', N'Japan', N'81');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (106, N'JE', N'Jersey', N'441534');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (107, N'JO', N'Jordan', N'962');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (108, N'KZ', N'Kazakhstan', N'7');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (109, N'KE', N'Kenya', N'254');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (110, N'KI', N'Kiribati', N'686');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (112, N'KW', N'Kuwait', N'965');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (113, N'KG', N'Kyrgyzstan', N'996');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (114, N'LA', N'Laos', N'856');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (115, N'LV', N'Latvia', N'371');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (116, N'LB', N'Lebanon', N'961');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (117, N'LS', N'Lesotho', N'266');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (118, N'LR', N'Liberia', N'231');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (119, N'LY', N'Libya', N'218');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (120, N'LI', N'Liechtenstein', N'423');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (121, N'LT', N'Lithuania', N'370');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (122, N'LU', N'Luxembourg', N'352');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (123, N'MO', N'Macau', N'853');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (124, N'MK', N'Macedonia', N'389');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (125, N'MG', N'Madagascar', N'261');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (126, N'MW', N'Malawi', N'265');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (127, N'MY', N'Malaysia', N'60');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (128, N'MV', N'Maldives', N'960');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (129, N'ML', N'Mali', N'223');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (130, N'MT', N'Malta', N'356');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (131, N'MH', N'Marshall Islands', N'692');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (132, N'MR', N'Mauritania', N'222');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (133, N'MU', N'Mauritius', N'230');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (134, N'YT', N'Mayotte', N'262');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (135, N'MX', N'Mexico', N'52');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (136, N'FM', N'Micronesia', N'691');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (137, N'MD', N'Moldova', N'373');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (138, N'MC', N'Monaco', N'377');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (139, N'MN', N'Mongolia', N'976');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (140, N'ME', N'Montenegro', N'382');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (141, N'MS', N'Montserrat', N'1664');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (142, N'MA', N'Morocco', N'212');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (143, N'MZ', N'Mozambique', N'258');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (144, N'NA', N'Namibia', N'264');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (145, N'NR', N'Nauru', N'674');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (146, N'NP', N'Nepal', N'977');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (147, N'NL', N'Netherlands', N'31');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (148, N'AN', N'Netherlands Antilles', N'599');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (149, N'NC', N'New Caledonia', N'687');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (150, N'NZ', N'New Zealand', N'64');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (151, N'NI', N'Nicaragua', N'505');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (152, N'NE', N'Niger', N'227');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (153, N'NG', N'Nigeria', N'234');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (154, N'NU', N'Niue', N'683');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (156, N'KP', N'Korea, North', N'850');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (157, N'MP', N'Northern Mariana Islands', N'1670');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (158, N'NO', N'Norway', N'47');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (159, N'OM', N'Oman', N'968');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (160, N'PK', N'Pakistan', N'92');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (161, N'PW', N'Palau', N'680');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (162, N'PA', N'Panama', N'507');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (163, N'PG', N'Papua New Guinea', N'675');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (164, N'PY', N'Paraguay', N'595');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (165, N'PE', N'Peru', N'51');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (166, N'PH', N'Philippines', N'63');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (167, N'PN', N'Pitcairn Islands', N'64');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (168, N'PL', N'Poland', N'48');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (169, N'PT', N'Portugal', N'351');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (170, N'PR', N'Puerto Rico', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (171, N'QA', N'Qatar', N'974');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (172, N'CG', N'Congo', N'242');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (173, N'RO', N'Romania', N'40');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (174, N'RU', N'Russia', N'7');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (175, N'RW', N'Rwanda', N'250');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (176, N'BL', N'St. Barthelemy', N'590');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (177, N'SH', N'St. Helena', N'290');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (178, N'KN', N'St. Kitts and Nevis', N'1869');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (179, N'LC', N'St. Lucia', N'1758');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (180, N'MF', N'St. Martin', N'590');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (181, N'PM', N'St. Pierre and Miquelon', N'508');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (182, N'VC', N'St. Vincent and the Grenadines', N'1784');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (183, N'WS', N'Samoa', N'685');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (184, N'SM', N'San Marino', N'378');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (185, N'ST', N'Sao Tome and Principe', N'239');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (186, N'SA', N'Saudi Arabia', N'966');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (187, N'SN', N'Senegal', N'221');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (188, N'RS', N'Serbia', N'381');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (189, N'SC', N'Seychelles', N'248');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (190, N'SL', N'Sierra Leone', N'232');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (191, N'SG', N'Singapore', N'65');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (192, N'SK', N'Slovakia', N'421');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (193, N'SI', N'Slovenia', N'386');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (194, N'SB', N'Solomon Islands', N'677');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (195, N'SO', N'SOMALIA MOGADISHU', N'252');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (196, N'ZA', N'South Africa', N'27');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (197, N'KR', N'Korea, South', N'82');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (198, N'ES', N'Spain', N'34');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (199, N'LK', N'Sri Lanka', N'94');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (200, N'SD', N'Sudan', N'249');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (201, N'SR', N'Suriname', N'597');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (202, N'SJ', N'Svalbard', N'47');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (203, N'SZ', N'Swaziland', N'268');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (204, N'SE', N'Sweden', N'46');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (205, N'CH', N'Switzerland', N'41');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (206, N'SY', N'Syria', N'963');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (207, N'TW', N'Taiwan', N'886');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (208, N'TJ', N'Tajikistan', N'992');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (209, N'TZ', N'Tanzania', N'255');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (210, N'TH', N'Thailand', N'66');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (211, N'TL', N'East Timor', N'670');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (212, N'TG', N'Togo', N'228');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (213, N'TK', N'Tokelau', N'690');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (214, N'TO', N'Tonga', N'676');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (215, N'TT', N'Trinidad and Tobago', N'1868');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (216, N'TN', N'Tunisia', N'216');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (217, N'TR', N'Turkey', N'90');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (218, N'TM', N'Turkmenistan', N'993');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (219, N'TC', N'Turks and Caicos Islands', N'1649');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (220, N'TV', N'Tuvalu', N'688');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (221, N'UG', N'Uganda', N'256');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (222, N'UA', N'Ukraine', N'380');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (223, N'AE', N'United Arab Emirates', N'971');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (224, N'GB', N'United Kingdom', N'44');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (225, N'US', N'United States', N'1');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (226, N'UY', N'Uruguay', N'598');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (228, N'UZ', N'Uzbekistan', N'998');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (229, N'VU', N'Vanuatu', N'678');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (230, N'VE', N'Venezuela', N'58');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (231, N'VN', N'Vietnam', N'84');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (232, N'WF', N'Wallis and Futuna', N'681');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (234, N'EH', N'Western Sahara', N'212');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (235, N'YE', N'Yemen', N'967');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (236, N'ZM', N'Zambia', N'260');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (237, N'ZW', N'Zimbabwe', N'263');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (238, N'NFK', N'Norfolk Island', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (242, NULL, N'Bonaire', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (243, NULL, N'Canary Islands, The', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (244, N'VG', N'Virgin Islands (British)', N'1284');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (245, N'VI', N'Virgin Islands (U.S.)', N'1340');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (246, N'CW', N'Curacao', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (247, N'GP', N'Guadeloupe', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (248, N'GG', N'Guernsey', N'441481');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (249, N'XK', N'Kosovo', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (250, N'MQ', N'Martinique', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (251, NULL, N'Nevis', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (252, N'RE', N'Reunion', N'262');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (253, NULL, N'Saipan', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (254, NULL, N'St. Eustatius', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (255, NULL, N'Somaliland (North Somalia)', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (256, NULL, N'Tahiti', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (257, N'GF', N'French Guyana', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (258, N'CN', N'China', N'86');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (259, N'PF', N'French Polynesia', N'689');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (260, NULL, N'Tortola', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (261, N'YG', N'Yugoslavia', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (262, NULL, N'Zaire', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (263, N'SX', N'St. Maarten', N'1721');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (264, N'AX', N'Aland Island', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (265, N'PT-20', N'Azores', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (266, NULL, N'Buesingen', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (267, NULL, N'Campione / Lake Lugano', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (268, N'ES-CE', N'Ceuta', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (269, N'EN', N'England', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (270, NULL, N'Heligoland', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (271, NULL, N'Kosrae', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (272, NULL, N'Livigno', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (273, N'PT-30', N'Madeira', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (274, N'ES-ML', N'Melilla', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (275, NULL, N'Mount Athos', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (276, N'NIR', N'Northern Ireland', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (277, NULL, N'Ponape', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (278, NULL, N'Rota', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (279, NULL, N'Sapa', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (280, N'SCT', N'Scotland', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (281, NULL, N'St. Christopher', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (282, NULL, N'St. Croix', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (283, NULL, N'St. John', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (284, NULL, N'St. Thomas', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (285, NULL, N'Tinian', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (286, NULL, N'Truk', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (287, NULL, N'Union Island', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (288, N'VA', N'Vatican City', N'379');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (289, NULL, N'Virgin Gorda', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (290, N'WLS', N'Wales', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (291, NULL, N'Yap', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (292, N'PS', N'Palestine', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (297, N'SGVN', N'Sin-Viet', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (298, N'RTSIN', N'RT To Singapore', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (299, N'RTDXB', N'RT To Dubai', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (300, N'PSDC', N'Phát sinh đơn cũ', NULL);
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (301, N'XS', N'SOMALIA HARGEISA', N'');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (302, N'RT-DE', N'RT to Germany', N'');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (303, N'RT-GB', N'RT to UK', N'');
    INSERT [dbo].[Countries] ([CountryID], [CountryCode], [CountryName], [CountryPhone]) VALUES (304, N'RT-AU', N'RT to Australia', N'');
    SET IDENTITY_INSERT dbo.Countries OFF;
END
GO
