# CMS và nhân viên Việt An phụ trách khách

Ghi chú ngày 16/10/2026. Các luồng dưới đây **sẽ làm ở CMS** — trang tổng của nhân viên Việt An (giám đốc đến nhân viên, đăng nhập bằng tài khoản `dbo.USERS`). Portal khách hàng chỉ chuẩn bị dữ liệu và quy ước ghi để CMS chạy song song trên **cùng database `vietanexpress`**.

> Phân biệt: **tài khoản con / nhân viên của khách** (bảng `TCustomer_NguoiLienLac`, trang Tài khoản nhân viên trên portal) khác với **nhân viên Việt An** (`dbo.USERS`, dùng ở CMS).

## Dữ liệu có sẵn

| Bảng / cột | Ý nghĩa |
|---|---|
| `dbo.TCustomer.Staff_ID` (int, NOT NULL) | Nhân viên Việt An phụ trách khách → `dbo.USERS.ID`. `0` = chưa phân công |
| `dbo.USERS` | Nhân viên Việt An: `USER_NAME`, `FIRST_NAME` + `LAST_NAME`, `EMAIL1`, `PHONE_MOBILE` / `PHONE_WORK`, `STATUS` (`ACTIVE`), `DELETED`, `IS_ADMIN`, `VIEW_CUSTOMER_ALL` (1 = xem mọi khách), `BRANCH_ID` |

Dữ liệu hiện tại: khách 200877 (SCS) → `son.hieu`; khách 201008 (SGB) → `vo.hanh`. Email / SĐT của hai nhân viên đang trống, ô `PHONE_MOBILE` của `son.hieu` lại chứa ngày sinh (`01/01/1987`).

## Đã chuẩn bị trong portal (chỉ đọc, không đổi cấu trúc)

- Module Customers map `TCustomer.Staff_ID` và `dbo.USERS` (không map cột mật khẩu): `Infrastructure/LegacyCustomer.cs`, `Infrastructure/VietAnUser.cs`.
- `ICustomersApi.GetAccountManagerAsync(customerId)` và `GetAccountManagersAsync(customerIds)` trả `AccountManager(Id, UserName, FullName, Email, Phone, BranchId, Active)`. Nhân viên `DELETED` hoặc `Staff_ID = 0` → coi như chưa phân công. SĐT chỉ nhận chuỗi giống số điện thoại (≥ 8 chữ số, không có `/`).
- **Chưa hiển thị ở đâu, chưa đổi luồng email.** Bật từng luồng bên dưới khi CMS sẵn sàng.

## Các luồng sẽ làm

### 1. Khách thấy nhân viên phụ trách (portal)
Trang Tổng quan và cột Liên hệ nhanh của Hỗ trợ & sự cố hiện tên, SĐT, email của nhân viên phụ trách; chưa phân công thì hiện hotline chung. Cần `USERS.EMAIL1` / `PHONE_*` được nhập đúng trên CMS trước.

### 2. Yêu cầu hỗ trợ / sự cố tới đúng người
Khi khách gửi yêu cầu (`dbo.TContact`, `FormType` = "Portal - …"), email báo gửi tới `EMAIL1` của nhân viên phụ trách và CC hộp thư chung. Không cần lưu thêm cột: CMS lọc theo người phụ trách bằng `TContact.CustID → TCustomer.Staff_ID`.

### 3. CMS: nhân viên chỉ thấy khách mình phụ trách
Đăng nhập CMS bằng `dbo.USERS`. Phạm vi dữ liệu:
- `IS_ADMIN = 1` hoặc `VIEW_CUSTOMER_ALL = 1` → mọi khách.
- Còn lại → khách có `TCustomer.Staff_ID = USERS.ID`.

Áp cho vận đơn (`MaVanDon.CustomerID`), yêu cầu hỗ trợ (`TContact.CustID`), tài khoản con (`TCustomer_NguoiLienLac.CustomerID`). Trang `/admin` hiện tại của portal (1 tài khoản cố định) sẽ được thay bằng CMS.

## Quy ước dữ liệu portal ghi — CMS cần đọc đúng

| Dữ liệu | Portal ghi | CMS nên |
|---|---|---|
| Đơn chưa in | `MaVanDon.Status = 0`, `OrderNumber` NULL, `VA_Bill` = tiền tố chi nhánh (vd `VAHN`) | Hiện ở danh sách đơn, cho sửa / xóa; **không** coi là đơn đã đi |
| Đơn đã in | `Status = 1`, `OrderNumber` + `VA_Bill` đầy đủ (vd `VAHCM6003585US`) | Như đơn hệ thống cũ |
| Người tạo đơn là tài khoản con | `MaVanDon.Customer_Staff_ID` = `TCustomer_NguoiLienLac.ID` (0 = tài khoản chính / hệ thống cũ); bảng `VanDonNguoiTao` đã bỏ 16/10/2026 | CMS hiển thị / lọc đơn theo nhân viên của khách bằng cột này |
| Yêu cầu hỗ trợ & sự cố | `TContact`: `FormType` "Portal - Sự cố / Hỗ trợ / Góp ý", `Status` 0 mới · 1 đang xử lý · 2 chờ khách · 3 đã xử lý, `Des01` vận đơn · `Des02` mức độ · `Des03` người nhận · `Des04` nước · `Des05` ID tài khoản con, `KeyWord` loại / chủ đề | Đổi `Status` để khách thấy trạng thái. Đổi trên CMS chưa tự tạo thông báo cho khách — cần CMS ghi `dbo.ThongBao` (CustomerID = khách) hoặc gọi API portal |
| Tài khoản con của khách | `TCustomer_NguoiLienLac`: `TinhCach` tên đăng nhập, `Mo_Ta` mật khẩu (như hệ thống cũ), `Quyen`, `Trang_Thai` 1 = hoạt động | Không xóa dòng có `TinhCach`; khóa thì đặt `Trang_Thai` ≠ 1 |
| MyTracking | `TCustomer_MyTracking` (1 dòng / khách, `Duong_Dan` không trùng) + `_Banner` | Không tạo 2 dòng cho cùng khách |

## Cần chốt trước khi làm CMS

1. Cách hệ thống cũ lưu mật khẩu `USERS.USER_PASSWORD` / `USER_HASH` (luồng 3).
2. `BRANCH_ID` ứng với chi nhánh nào (khớp tiền tố bill `VAHCM`, `VAHN`, …).
3. CS đổi trạng thái yêu cầu trên CMS thì báo khách bằng cách nào (ghi `ThongBao` hay gọi API portal).
4. Ghi đồng thời: portal đã khóa dòng `MaVanDon` (UPDLOCK) khi in / sửa / xóa đơn chưa in và khóa ứng dụng `portal:staff-account` khi tạo tài khoản con (16/10/2026). CMS sửa / xóa đơn `Status = 0` hoặc cấp số bill cũng nên đọc dòng với `UPDLOCK` trong transaction để phải chờ portal, và kiểm tra lại `Status` trước khi ghi.
