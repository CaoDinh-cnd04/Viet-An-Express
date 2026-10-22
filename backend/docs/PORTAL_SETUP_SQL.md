# File SQL dựng portal hiện hành

File chạy: [setup-portal-current.sql](../scripts/setup-portal-current.sql).

File này tiếp phần SQL còn dở trong phiên Claude Code ngày 15/10/2026, theo yêu cầu cuối: **bỏ các bảng đã nạp không thay đổi cấu trúc**.

## Phạm vi

Tạo 9 bảng nghiệp vụ portal nếu chưa có:

| Nhóm | Bảng |
|---|---|
| Vận đơn / danh mục | `NhomHangHoa`, `MatHangKhachHang`, `VanDonNguoiTao` (không còn dùng từ 16/10/2026 — người tạo vận đơn ghi ở `MaVanDon.Customer_Staff_ID`; `update-database.ps1` áp migration chuyển dữ liệu và xóa bảng) |
| E-commerce | `KenhTMDT`, `KetNoiTMDT`, `DonTMDT` |
| Thông báo / thống kê | `ThongBao`, `ThongBao_DaDoc`, `ThongKeSuDung` |

Bổ sung cấu trúc trên 5 bảng đã nạp, không tạo lại bảng:

| Bảng | Phần thay đổi |
|---|---|
| `MaVanDon` | Thêm `ConsigneeIossNo`, `ConsigneeEoriNo`, `VA_Bill` nếu thiếu |
| `MaVanDon_ChiTietHang` | Thêm `Kien` (int, NULL = invoice chung) nếu thiếu — invoice riêng từng kiện, thêm 20/10/2026; database đã dựng thì chạy `update-2026-10-20-invoice-kien.sql` |
| `TCustomer_NguoiLienLac` | Thêm `Quyen` nếu thiếu |
| `TCustomer_MyTracking` | Thêm slug, trạng thái xuất bản, hình nền, JSON bổ sung, ngày tạo/sửa; mở rộng 11 cột nội dung/thương hiệu sang `nvarchar(max)`; index khách và slug |
| `TCustomer_MyTracking_Banner` | Thêm JSON bổ sung; mở rộng 5 cột ảnh/liên kết/nhãn; index trang và thứ tự |

Giữ nguyên và không chứa lệnh tạo/đổi cấu trúc của 7 bảng đã nạp còn lại: `TCustomer`, `USERS`, `MaVanDon_PCS_DIM`, `VungXauVungXa`, `Catalog_HsCode`, `TContact`, `Countries`. Không nạp lại dữ liệu quốc gia hoặc dữ liệu khách hàng.

Có danh mục mặc định 8 kênh theo model, sequence tương thích của module Shipments và 3 bảng lịch sử migration. Danh mục có rồi không bị ghi đè tên hoặc trạng thái do người vận hành đã sửa. Nếu ID và mã kênh xung đột với quy ước trong code, script dừng để đối chiếu.

## Cách chạy trong SSMS

1. Nạp đủ 12 bảng hệ thống cũ vào database đích trước. Các bảng bổ sung từ script cũ `legacy-tables.sql` cũng phải được nạp trước; file hiện hành không lặp lại phần tạo chúng.
2. Chọn đúng database **vietanexpress** trong danh sách database của cửa sổ query. File không có `USE` hoặc `CREATE DATABASE`; nó chỉ làm việc trên database đang chọn và chặn các database hệ thống.
3. Mở **setup-portal-current.sql**, chạy toàn bộ bằng **F5**.
4. Kết thúc thành công sẽ có thông báo: `Đã hoàn tất schema portal hiện hành...`.

Script dùng một transaction với `XACT_ABORT`, `TRY/CATCH` và application lock. Nếu có lỗi, thay đổi của lần chạy được rollback. Có thể chạy lại toàn bộ file; không cần tự bỏ từng phần.

Trước khi ghi baseline, script đối chiếu 144 cột và 29 index, gồm cột tạo khóa chính/khóa duy nhất, cùng các ràng buộc và khóa ngoại hiện hành. Nếu cấu trúc bảng có sẵn khác code, script báo lỗi thay vì ghi lịch sử giả rằng mọi thứ đã hoàn tất.

Baseline ghi 36 migration hiện tại: Shipments 13, Ecommerce 7, Identity 16, EF Core 10.0.12. Đây là xác nhận cấu trúc cuối đã được kiểm tra; không chạy lại các bước lịch sử từng tạo rồi xóa bảng cũ.

## Database portal đời trước

Nếu còn `OrderDrafts`, `TaiKhoanNhanVien`, `MyTrackingCauHinh`, `SuCo`, `GopY` hoặc các bảng portal đã bỏ, file này chủ động dừng. Với trường hợp đó, dùng migration của repository (`update-database.ps1`) sau khi kiểm tra dữ liệu và bản sao lưu. Không ghi baseline để bỏ qua các bước chuyển dữ liệu.

File này là SQL cấu trúc theo code hiện hành. Nó không sửa các lỗi ghi đồng thời (in / sửa / xóa cùng một đơn nháp, tạo trùng tên đăng nhập nhân viên) hoặc cách đọc NULL của người liên hệ, và không chuẩn hóa dữ liệu đăng nhập cũ.
