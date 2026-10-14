# Copy đơn

Nút **Copy đơn** ở bảng và chi tiết đơn mở `/orders/new?from=<bill>`.
Trang tạo đơn điền đầy đủ thông tin người gửi/nhận, dịch vụ/hub, tham chiếu, loại hàng,
số kiện/cân thực, kích thước/cân/nhóm hàng từng dòng, các dòng invoice, nhà sản xuất,
HS/xuất xứ, loại tiền, hình thức xuất khẩu/thuế, shipping fee và dịch vụ cộng thêm.
Khách có thể sửa rồi bấm tạo đơn mới. Copy không ghi hoặc cập nhật đơn gốc.

`GET /api/v1/orders/{bill}/copy` yêu cầu quyền `shipments.view` và `shipments.create`.
Backend kiểm tra quyền xem đơn theo CustomerId và phạm vi tài khoản con trước khi lấy dữ liệu.
Ưu tiên bản form gốc trong nháp đã in (kể cả nháp bị xóa mềm), nhờ đó giữ được các trường
không có cột tương ứng trong bảng cũ, như addons, nhóm hàng, DDP và số đo thập phân.
Đơn hệ thống cũ không có form gốc hoặc có form hỏng được dựng từ các bảng vận đơn,
kiện và invoice; các thông tin hệ thống chưa từng lưu không thể phục dựng.

Frontend bổ sung mặc định chỉ cho trường thiếu, giữ tham chiếu và giá trị danh mục cũ.
Sửa bản copy dùng sessionStorage riêng theo tài khoản + bill, nên tải lại/đổi chế độ không
ghi đè bằng dữ liệu gốc hoặc dữ liệu một đơn khác. Tải lỗi hiện thông báo và nút Thử lại;
không hiển thị form rỗng để người dùng vô tình gửi.

Lưu bản copy gọi **POST /drafts**, không dùng draft ID/bill ID của đơn gốc.
Không copy mã vận đơn, tracking, trạng thái phát hàng, POD, lịch sử vận chuyển hoặc ảnh
chứng từ vào đơn mới; đây là dữ liệu phát sinh sau khi tạo đơn, không phải thông tin form.

Không cần migration. Cần triển khai cả API và web.

Kiểm thử: `copy-order.test.ts`, `autosaveKey.test.ts`, backend `CopyOrderTests.cs` và
`web/tests/browser/copy-order.cjs` (Playwright, toàn bộ API được stub).
