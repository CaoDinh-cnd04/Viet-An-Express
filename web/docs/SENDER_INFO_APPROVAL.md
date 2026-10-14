# Xét duyệt sửa thông tin người gửi

## Tiến độ và phạm vi

Khi kiểm tra, phần Claude Code đã có API, mô hình yêu cầu, migration tạo bảng,
quyền tài khoản chính/admin, thông báo và kiểm thử backend. Chưa có trang khách
hàng, menu và giao diện xét duyệt cho admin.

Phần hoàn thiện bổ sung giao diện hai phía, đồng bộ hồ sơ đã duyệt với phiên đăng
nhập, liên kết từ trang tạo đơn, bản dịch tiếng Anh và bảo vệ khi xử lý đồng thời.

## Cách dùng

- Khách hàng: **Tài khoản → Thông tin người gửi** (`/account/sender-info`).
  Tài khoản chính có thể đề nghị sửa tên công ty, người liên hệ, điện thoại,
  email, địa chỉ lấy hàng và mã số thuế, kèm ghi chú.
- Mỗi khách có tối đa một yêu cầu chờ duyệt. Có thể hủy yêu cầu đó để gửi lại.
  Hồ sơ hiện tại giữ nguyên cho đến khi admin duyệt. Trang hiển thị yêu cầu chờ
  duyệt và 10 yêu cầu đã xử lý gần nhất, gồm lý do từ chối.
- Admin: **Duyệt người gửi** trong `/admin`. Xem thông tin cũ, đề nghị mới và
  hồ sơ hiện tại; duyệt hoặc từ chối với lý do bắt buộc. Có bộ lọc trạng thái.
- Khi duyệt, chỉ cập nhật các trường khách đề nghị thay đổi. Nếu những trường
  đó đã đổi trong hệ thống trong lúc chờ, yêu cầu không được duyệt; khách cần
  hủy và gửi lại theo hồ sơ hiện tại.
- Sau xét duyệt, khách nhận thông báo trong portal và email nếu có địa chỉ hợp
  lệ/cấu hình gửi mail hoạt động. Hồ sơ được cập nhật vào phiên đăng nhập khi
  trang người gửi tải lại; đơn mới dùng hồ sơ đã duyệt. Đơn cũ giữ dữ liệu cũ.
- Tài khoản nhân viên không có quyền gửi yêu cầu sửa hồ sơ khách hàng.

## Cơ sở dữ liệu và triển khai

Thay đổi này mới nằm trong mã nguồn, chưa triển khai hoặc chạy migration lên
cơ sở dữ liệu thực. Cần áp dụng hai migration của `IdentityDbContext` theo thứ tự:

1. `20261013091550_AddSenderInfoRequests`: tạo bảng lưu yêu cầu và lịch sử.
2. `20261013094827_EnsureSenderReviewConsistency`: tạo chỉ mục duy nhất có
   điều kiện để mỗi khách chỉ có một yêu cầu `pending`.

Nếu bảng đã có dữ liệu, kiểm tra/xử lý các yêu cầu chờ trùng khách trước khi
áp dụng migration thứ hai. Không tự động xóa lịch sử.

Khách và admin đang đăng nhập bằng token cũ cần đăng xuất/đăng nhập lại để
nhận quyền `account.sender-info` và `admin.sender-info`. Admin dùng cơ chế
đăng nhập quản trị hiện có.

Phê duyệt dùng cùng một giao dịch SQL cho hồ sơ `dbo.TCustomer`, trạng thái
yêu cầu và thông báo; khóa bản ghi trong lúc kiểm tra/cập nhật. Trạng thái yêu
cầu có concurrency token để tránh hủy/từ chối ghi đè thao tác đã xử lý.
Email kết quả chỉ đưa vào hàng đợi sau khi ghi thành công.

## Kiểm tra

- Bộ kiểm thử Identity: 122 kiểm thử đạt, gồm quyền truy cập, validation,
  xử lý yêu cầu, hồ sơ cũ, lịch sử và cấu hình chỉ mục/concurrency.
- Frontend build và kiểm thử quyền/bản dịch đạt.
- `web/tests/browser/sender-info.cjs`: trình duyệt Chromium, API giả lập;
  kiểm tra gửi, hủy, từ chối, duyệt, lịch sử, giao diện điện thoại, hồ sơ đã
  duyệt điền vào đơn mới, hồ sơ thay đổi khi chờ và chặn tài khoản nhân viên.
  Chạy với `PLAYWRIGHT_MODULE`, `CHROMIUM_PATH`, `TEST_BASE_URL` nếu cần.
- Chưa chạy kiểm thử tích hợp giao dịch/khóa trên SQL Server thực hoặc thử
  gửi email thực. Cần kiểm tra hai phần này ở môi trường triển khai.
