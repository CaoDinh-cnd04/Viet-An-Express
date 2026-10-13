# Thống kê sử dụng

Admin mở `/admin` → **Thống kê sử dụng**. Có bộ lọc ngày (giờ Việt Nam), loại tài khoản,
lượt xem và người truy cập riêng biệt, xếp hạng trang, dữ liệu theo ngày,
so sánh tạo đơn từng bước và tạo đơn một trang.

## Triển khai

- Triển khai cả backend và frontend.
- Migration Identity `AddUsageAnalytics` tạo bảng `dbo.ThongKeSuDung` và hai index.
  Backend tự migrate khi khởi động nếu `Database:RunStartupTasks=true` theo cấu hình hiện có.
  Nếu tắt tác vụ khởi động, áp dụng migration Identity bằng quy trình triển khai database hiện tại.
- Admin đang đăng nhập cần đăng xuất / đăng nhập lại để token có quyền `admin.analytics`.
- Chỉ có dữ liệu từ khi bật tính năng; không phục dựng lịch sử trước đó.

## Cách tính

- `page_view`: mỗi lần vào route; StrictMode không gửi trùng. URL tra cứu công khai được
  gộp thành `/tracking` hoặc `/t`; không gửi query string, mã vận đơn hay slug khách.
- Người riêng biệt: khách đăng nhập theo CustomerId + StaffId do backend xác định;
  chưa đăng nhập theo UUID trình duyệt. Không phải số người vật lý; đăng nhập khác tài khoản,
  dùng nhiều trình duyệt hoặc xóa storage có thể tăng số đếm.
- `order_start`: thay đổi trường form mới lần đầu; mỗi flow + chế độ chỉ ghi một lần.
  Flow giữ trong sessionStorage khi đổi chế độ hoặc tải lại; tạo thành công thì đặt lại flow.
- `order_complete`: chỉ gửi sau khi API lưu `status=ready` thành công. Không phải in/cấp bill.
  Sửa đơn nháp mở bằng `?draft=` không ghi sự kiện tạo đơn. Lưu nháp không tính thành công.
- `mode_switch`: chuyển trực tiếp giữa hai route tạo đơn, gồm nút chọn chế độ, menu, Back/Forward.
  Thống kê cho chế độ đích. Không tính thao tác trên form sửa đơn nháp.
- Báo cáo khử trùng flow khi đếm bắt đầu/thành công. Form đi qua hai chế độ có thể thuộc
  cả hai mẫu số; thành công thuộc chế độ đang dùng khi lưu. Mẫu số bao gồm flow bắt đầu
  hoặc hoàn thành trong khoảng ngày, không phải phân tích cohort.
- Ngày `from` / `to` tính cả hai đầu theo UTC+7, tối đa 366 ngày.

## API và lưu dữ liệu

- `POST /api/v1/usage/events`: `{ id, visitorId, path, kind, mode?, flowId? }`.
  Body tối đa 2 KB, 120 request/phút/IP. Route, kind, mode được kiểm tra allowlist;
  các sự kiện tạo đơn yêu cầu phiên khách hợp lệ. Audience và thời điểm lấy từ máy chủ.
- `GET /api/v1/admin/analytics?from=YYYY-MM-DD&to=YYYY-MM-DD&audience=all`:
  yêu cầu quyền `admin.analytics`; audience nhận `all`, `customer`, `staff`, `anonymous`.
  Trả `{ data: { views, visitors, pages, modes, days } }`.
- Event ID là khóa chính, retry không tạo thêm bản ghi. Frontend retry tối đa hai lần
  khi mạng lỗi, 429 hoặc 5xx. Lỗi thống kê không chặn điều hướng/tạo đơn.
- Không lưu dữ liệu form, địa chỉ, điện thoại, email hoặc nội dung URL riêng tư.
  Chưa có tác vụ tự xóa dữ liệu thống kê.
- Đây là thống kê hành vi từ trình duyệt; mất mạng/chặn script có thể thiếu lượt ghi nhận,
  không dùng thay báo cáo đơn hàng hoặc kế toán.

## Kiểm tra

- Backend: `dotnet test --project backend/tests/VietAnExpress.Identity.Tests/VietAnExpress.Identity.Tests.csproj`.
- Frontend: trong `web`, `npm run build` và `npm test -- src/shared/lib/usage.test.ts`.
- Browser: build và chạy preview, rồi `node web/tests/browser/usage.cjs`.
  `PLAYWRIGHT_MODULE` trỏ tới playwright/playwright-core đã cài; `CHROMIUM_PATH` tùy chọn;
  `TEST_BASE_URL` mặc định `http://127.0.0.1:4178`. Toàn bộ API được stub, không ghi DB thật.
