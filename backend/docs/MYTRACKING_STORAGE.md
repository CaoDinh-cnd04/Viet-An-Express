# MyTracking dùng bảng hệ thống cũ

Portal đọc / ghi `dbo.TCustomer_MyTracking` và `dbo.TCustomer_MyTracking_Banner`. API và đường dẫn `/t/{slug}` giữ nguyên.

## Ánh xạ thông tin trang

| Dữ liệu API | Cột trong TCustomer_MyTracking |
|---|---|
| Khách sở hữu | `CustomerID` → `TCustomer.CustomerID` |
| `slug` | `Duong_Dan` (bổ sung), unique khi không NULL |
| `published` | `Da_Xuat_Ban` (bổ sung), mặc định false |
| `config.title` / `description` | `UuDai_TieuDe` / `UuDai_NoiDung` |
| `brand.companyName` / `address` / `phone` / `logo` | `CompanyName` / `Address` / `Tel` / `Logo` |
| `brand.facebook` / `instagram` / `x` | `CongTruyenThong_01` / `_02` / `_03` |
| `brand.zalo` / `whatsapp` | `CongTinNhan_01` / `_02` |
| `background` | `Hinh_Nen` (bổ sung) |
| Thuộc tính JSON khác | `Cau_Hinh_Bo_Sung` (bổ sung); giữ các thuộc tính chưa có cột |
| Ngày tạo / sửa | `CreateDate` / `ModifyDate` (bổ sung) |

Khóa chính là `ID int IDENTITY`, không phải `CustomerID`. Có unique index cho `CustomerID` để mỗi khách chỉ có một trang.
Bản ghi do phần mềm cũ tạo chưa có slug không tự được xuất bản; khi khách mở phần cấu hình, portal gợi ý slug như trước.
Những cột cũ khác không được portal ánh xạ vẫn giữ nguyên.

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
| Thuộc tính JSON khác của ảnh | `Cau_Hinh_Bo_Sung` (bổ sung) |

Các cột văn bản do portal sử dụng được mở rộng từ `nvarchar(250)` sang `nvarchar(max)` để giữ được ảnh data URL và mọi giá trị JSON đã lưu trước đây. Cột slug vẫn giới hạn `varchar(60)`. Giới hạn dữ liệu gửi API vẫn là 2 MB.

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

- Unit test kiểm tra lưu / đọc đủ cấu hình, ảnh dài, thuộc tính bổ sung và thay thế / xóa ảnh.
- Kiểm thử SQL Server dùng database tạm `vietan_tests_mytracking_<guid>` để xác minh migration trên đúng DDL bảng cũ, giữ cấu hình đầy đủ và không ghi đè dữ liệu đích bị trùng khách. Chạy bằng `VIETAN_TEST_SQLSERVER` theo hướng dẫn trong README.
