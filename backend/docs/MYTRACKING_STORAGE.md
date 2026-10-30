# MyTracking dùng bảng hệ thống cũ

Như trang mytracking.top cũ (đối chiếu HTML trang cũ 30/10/2026): **thương hiệu + cổng liên lạc nằm ở hồ sơ khách `dbo.TCustomer`**,
phần mở rộng (ưu đãi, ảnh nền, mạng xã hội, ảnh quảng cáo) ở `dbo.TCustomer_MyTracking` + `dbo.TCustomer_MyTracking_Banner`.
Portal **giữ cấu trúc và độ dài cột gốc**. Ảnh nằm trong thư mục `Upload`, SQL chỉ giữ đường dẫn. Khách nào cũng có trang công khai
(thương hiệu từ hồ sơ khách; bảng cũ không có trạng thái xuất bản). Link công khai: `/tracking/{mã khách}/{số bill}?ref={mã REF}`.
Trang cũ chỉ hiện phần mở rộng khi `TCustomer.ViewLinkTracking = 1`; portal chỉ dùng cột này để mở trang cấu hình.

## Thương hiệu + cổng liên lạc (`MyTracking/MyTrackingBrand.cs`)

| Dữ liệu API | Cột trong TCustomer |
|---|---|
| `brand.companyName` | `InvoiceCustomerName`, trống thì `CustomerName` — chỉ đọc |
| `brand.address` / `phone` | `Address1` / `Phone` — **chỉ đọc** (thông tin người gửi trên bill; đổi qua yêu cầu hỗ trợ) |
| `brand.logo` | `Logo_ShippingMark` (bigint) → `TCommonFile.CommonFileID`; ảnh `/Upload/Logo_ShippingMark/{ServerFileName}`. Cũng là shipping mark in trên bill (`p_MVD_Print_Bill_Multi`). Logo mới → thêm 1 dòng `TCommonFile` (`RealFileName` = `ServerFileName` = tên tệp theo mã băm, `ServerFilePath` `/Upload/Logo_ShippingMark/`, `FileSize`, `MimeType`); xóa logo → `Logo_ShippingMark` NULL (giữ dòng `TCommonFile`) |
| `brand.website` | `Bank` nvarchar(150) — nút Home của trang cũ |
| `brand.primaryColor` / `secondaryColor` | `Column1` / `Column2` (`--mcolor` / `--scolor` của trang cũ) |
| `brand.contacts` | `CongLienLac_Hotline` (100), `_Zalo`, `_Messenger`, `_WhatsApp`, `_WeChat`, `_Telegram`, `_Viber`, `_Line`, `_Snapchat`, `_KakaoTalk`, `_Signal` (= Instagram của portal — trang cũ hiện ô Signal bằng icon Instagram), `_Email` (200) |

EF map cùng dòng TCustomer với `CustomerLogin` (table splitting); cột `Staff_ID` (NOT NULL, chỉ đọc) giúp EF nhận ra dòng khi mọi cột thương hiệu đều NULL.

## Phần mở rộng (TCustomer_MyTracking)

| Dữ liệu API | Cột trong TCustomer_MyTracking |
|---|---|
| Khách sở hữu | `CustomerID` → `TCustomer.CustomerID` (portal giữ mỗi khách 1 trang bằng khóa transaction, không thêm unique index) |
| `config.title` / `description` | `UuDai_TieuDe` / `UuDai_NoiDung` |
| `brand.socials` (mạng xã hội, ≤ 5) | `CongTruyenThong_01` Facebook, `_02` Instagram, `_03` Youtube, `_04` Tiktok, `_05` Twitter, `_06` Google Maps, `_07` Pinterest |
| `background` | `Str1`, `nvarchar(250)`, đường dẫn ảnh nền/banner, vd `/Upload/Banner/{tên-file}.jpg` |

Thứ tự ô khớp trang mytracking.top cũ (`social_line01…07`) — `MyTrackingChannels.cs`; mỗi ô tối đa 250 ký tự. Thuộc tính JSON lạ không được giữ.
Cột thương hiệu của bảng này (`CompanyName`, `Address`, `Tel`, `Logo`, `Website`, `Color_*`, `Email`, `CongTinNhan_*`) trang cũ không dùng —
portal không đọc / ghi nữa (từ 30/10/2026; dữ liệu portal ghi trước đó còn nguyên, không chuyển sang TCustomer).
Chưa dùng: `Banner_QC`, `Str2`, `CongTruyenThong_08–15` (bảng chính); `ID_Hinh`, `Str4–6`,
`PhienBan_Tracking`, `TypeFile`, `FileName` (bảng ảnh) — chưa rõ cách hệ thống cũ dùng (code `Get_TrackingData` của trang cũ không có trong dump).

## Ánh xạ ảnh quảng cáo

| Dữ liệu API | Cột trong TCustomer_MyTracking_Banner |
|---|---|
| Liên kết trang | `MyTrackingID` → `TCustomer_MyTracking.ID` |
| Khách sở hữu | `CustomerID` |
| Thứ tự trong mảng `images` | `SortOrder`, bắt đầu từ 0 |
| `id` | `Str1` |
| `src` | `Url_Hinh` |
| `linkUrl` | `Link` |
| `title` / `buttonText` | `Str2` / `Str3` |

Độ dài cột giống DDL gốc: các cột `nvarchar` dài 250 ký tự; riêng `TypeFile` dài 50. `Banner_QC` giữ kiểu `ntext`.
Hai bảng chỉ có khóa chính như bản gốc. Backend từ chối giá trị vượt độ dài cột, gồm mô tả, địa chỉ, tên công ty và link ảnh.
Giới hạn dữ liệu gửi API 2 MB, mỗi ảnh tải lên tối đa 200 KB.

Ảnh tải lên được ghi thành tệp trong `Upload/Banner` hoặc `Upload/Logo_ShippingMark`; SQL giữ đường dẫn tương đối
`/Upload/...`. Data URL chỉ dùng khi truyền ảnh từ trình duyệt, không ghi vào SQL. Ảnh là tệp bất biến theo mã băm;
không xóa tệp cũ khi đổi cấu hình, tránh làm hỏng link hệ thống cũ đang dùng.
API trả link đọc ảnh của API cho tệp có trên đĩa; khi lưu lại, link này được chuyển về đường dẫn `/Upload/...`.
Đường dẫn ảnh cũ chưa có trên đĩa vẫn đọc từ `VITE_MYTRACKING_ASSET_URL` (mặc định `https://mytracking.top`).

Trên hosting, đặt `MyTracking__UploadDirectory` thành **đường dẫn vật lý tới thư mục Upload dùng chung với site cũ**
(ví dụ `D:\Sites\MyTracking\Upload`) và cấp quyền Modify cho Application Pool API. Khi đó cả hai site cùng đọc được ảnh mới.
Mặc định dùng `Upload` bên cạnh ứng dụng backend. Không ghi đè hoặc xóa thư mục này khi cập nhật bản publish.
`MyTracking__PublicApiBaseUrl` có thể đặt thành tên miền API công khai nếu máy chủ đi qua reverse proxy;
mặc định lấy scheme/host của request. `MyTracking__LegacyAssetBaseUrl` mặc định `https://mytracking.top`.

Thứ tự ảnh được tái dựng theo `SortOrder`, rồi `ID`. Khi lưu lại trang, các dòng banner trước đó của trang được xóa và ghi bộ ảnh mới trong cùng lần `SaveChanges`; không để lại ảnh mồ côi khi người dùng xóa ảnh.

## Chuyển dữ liệu

Migration `20261015093129_MoveMyTrackingToLegacyTables`:

1. Yêu cầu hai bảng đích đã tồn tại, được tạo bằng `scripts/legacy-tables.sql` nếu cần.
2. Dừng nếu bảng đích có trùng khách hoặc cùng khách đã có cấu hình ở cả nguồn và đích; không tự chọn dữ liệu để ghi đè.
3. Bổ sung metadata và mở rộng cột cần thiết, giữ nguyên các cột cũ khác.
4. Chuyển từng trang và từng ảnh, giữ slug, trạng thái xuất bản, ngày giờ, thứ tự ảnh và thuộc tính JSON bổ sung.
5. Kiểm tra đủ trang / ảnh / metadata, tạo các index và xóa `dbo.MyTrackingCauHinh` sau cùng. Các bước nằm trong transaction migration.

Khi áp trên database hiện tại, tạo bản sao lưu dữ liệu ba bảng trong thư mục `backup/mytracking/` (đã bị Git bỏ qua), rồi đối chiếu toàn bộ JSON tái dựng với bản sao lưu trong transaction trước khi xóa bảng nguồn.

Migration này chỉ chạy theo chiều chuyển sang bảng cũ. `Down` chủ động từ chối vì không thể tự xóa hoặc thu hẹp dữ liệu mà phần mềm cũ có thể đã chỉnh sửa sau đó. Nếu cần khôi phục, dùng bản sao lưu và quy trình chuyển dữ liệu có kiểm tra.

## Kiểm thử

- Unit test kiểm tra ảnh được ghi thành tệp, SQL chỉ giữ đường dẫn, đọc/lưu lại link API, giới hạn 250 và đọc ảnh cũ.
- Kiểm thử SQL Server dùng database tạm `vietan_tests_mytracking_<guid>` để xác minh migration trên đúng DDL bảng cũ, giữ cấu hình đầy đủ và không ghi đè dữ liệu đích bị trùng khách. Chạy bằng `VIETAN_TEST_SQLSERVER` theo hướng dẫn trong README.

Migration `20261023082705_TrimMyTrackingPortalColumns` (23/10/2026, người dùng yêu cầu):

1. Bản ghi portal lưu trước khi ghi đúng ô (có `Cau_Hinh_Bo_Sung`, chưa có `"columnMap":2`): chuyển Zalo `CongTinNhan_01` → `_02`,
   WhatsApp `_02` → `_04`, X `CongTruyenThong_03` → `_05`. Bản ghi phần mềm cũ giữ nguyên.
2. Xóa index `UX_TCustomer_MyTracking_Duong_Dan` và các cột portal thêm nay thừa: `Duong_Dan`, `Cau_Hinh_Bo_Sung` (2 bảng),
   `CreateDate`, `ModifyDate` (kèm ràng buộc mặc định). Cột không còn thì bỏ qua; `Down` không hoàn tác — dùng bản sao lưu.

Migration `20261023085751_MoveBackgroundToStr1DropPublish`: nới `Str1` lên `nvarchar(max)`, chép `Hinh_Nen` sang `Str1` (dòng `Str1` còn trống),
rồi xóa `Hinh_Nen` và `Da_Xuat_Ban`.

Migration tiếp theo `20261023095956_RestoreOriginalMyTrackingSchema` trả mọi cột `nvarchar` về đúng độ dài gốc,
kể cả `Str1`, và bỏ hai index portal thêm. Dừng và rollback nếu có dữ liệu quá dài; không cắt hoặc xóa dữ liệu.
SQL tương đương để chạy trên hosting: `scripts/restore-mytracking-original.sql` (có trong thư mục publish).

Ảnh hệ thống cũ lưu đường dẫn tương đối (`/Upload/…`, `/upload/Logo_ShippingMark/…`): frontend ghép với `VITE_MYTRACKING_ASSET_URL`
(mặc định `https://mytracking.top`). Khi tên miền mytracking.top chuyển sang portal mới phải chép thư mục `Upload` của trang cũ sang
site mới (hoặc trỏ biến này về nơi còn giữ ảnh), nếu không ảnh cũ sẽ mất.
