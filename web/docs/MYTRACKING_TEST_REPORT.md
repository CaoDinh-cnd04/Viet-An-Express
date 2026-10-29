# Kiểm thử MyTracking

Ngày: 02/10/2026. Kiểm thử trực tiếp trên Chromium bằng Playwright, dùng browser context và tài khoản thử riêng.

## Kết quả

- 25/25 nhóm thao tác trình duyệt đạt.
- Build TypeScript/Vite thành công; 176/176 unit test frontend đạt.
- Backend: 171/171 tests đạt ở cấu hình Release (`dotnet test --solution VietAnExpress.slnx --configuration Release --no-restore`). Dùng Release vì API đang chạy khóa các DLL trong Debug.
- Không phát sinh lỗi JavaScript trong các thao tác MyTracking.
- Đã kiểm tra thêm cả 4 logo Facebook/Instagram/X/Zalo tải và giải mã thành công; tên công ty/địa chỉ mẫu ở desktop 1600/1366/1200 px và nội dung dài ở điện thoại 390/320 px không tràn ngang.

## Phạm vi

Tải logo/ảnh quảng cáo/nền bằng file thật được giải mã và nén bằng canvas. Đã thử JPG, JPEG, PNG, WebP; ảnh đúng 200 KB, quá 200 KB, GIF, SVG và file hỏng. URL ảnh HTTP được tải từ Vite bằng hostname khác; URL lỗi 404, HTML, giao thức không hợp lệ và timeout được mô phỏng có kiểm soát.

Đã thao tác sửa thương hiệu/nội dung, tiêu đề/chữ nút/link từng ảnh, Lên/Xuống, kéo thả bằng chuột, xóa ảnh, giới hạn 5 ảnh, Zalo/WhatsApp và mạng xã hội, xem desktop/mobile, lưu/reload, khôi phục mặc định, dữ liệu cũ, JSON hỏng, bộ nhớ đầy/bị chặn và đổi khách.

API phiên khách được giả lập trong test. Test không đăng nhập tài khoản thật, không ghi database và không thử xuất bản: chức năng xuất bản chưa có API. Khi bộ nhớ bị chặn/đầy, bản cấu hình giữ trong phiên ứng dụng; tải lại toàn bộ trang làm mất bản chưa lưu đó, đúng cảnh báo hiện có.

## Lỗi đã sửa

Khôi phục mặc định trước đây reset cả mốc đã lưu, khiến form báo không có thay đổi chưa lưu. Đã giữ mốc đã lưu khi khôi phục; test xác nhận form báo có thay đổi, bản đã lưu vẫn giữ nguyên đến khi bấm Lưu. (28/10/2026: bỏ chế độ lưu localStorage — script giả lập API `/account/mytracking`.)

## Chạy lại

Script: `web/tests/browser/mytracking.cjs`. Cần Playwright được cài riêng và Chromium. Không thêm dependency vào ứng dụng.

```powershell
# Chạy Vite ở terminal riêng trong web/
npm.cmd run dev -- --host 127.0.0.1

# Từ thư mục gốc dự án; sửa các đường dẫn/cổng theo máy
$env:PLAYWRIGHT_MODULE = "$env:TEMP/vietan-ui-tests/node_modules/playwright"
$env:CHROMIUM_PATH = 'C:/Users/ASUS/AppData/Local/ms-playwright/chromium-1208/chrome-win64/chrome.exe'
$env:TEST_BASE_URL = 'http://127.0.0.1:5174'
node web/tests/browser/mytracking.cjs
```

Script lưu `results.json`, `desktop.png`, `mobile.png` và `desktop-mobile-preview.png` trong `%TEMP%/vietan-ui-tests/results` mặc định.

## Ảnh chụp

- [Desktop](testing/mytracking/desktop.png)
- [Mobile](testing/mytracking/mobile.png)
- [Chế độ preview điện thoại](testing/mytracking/desktop-mobile-preview.png)

## Chi tiết ca kiểm thử

| Ca kiểm thử | Kết quả |
|---|---|
| Nhập thương hiệu/nội dung cập nhật preview ngay | passed |
| Tải logo PNG, nén thành WebP | passed |
| Thay logo bằng URL và xóa logo | passed |
| Tải ảnh quảng cáo JPG/PNG/WebP bằng giải mã ảnh thật | passed |
| Thêm ảnh URL trực tiếp/Enter, đạt tối đa 5 ảnh | passed |
| Tiêu đề/chữ nút/link website; khóa nút khi link trống và ẩn nút khi chữ trống | passed |
| Đổi thứ tự bằng Lên/Xuống và kéo thả | passed |
| Xóa ảnh, mở lại thêm ảnh và giữ dữ liệu ảnh khác | passed |
| Chặn file quá 200 KB, GIF/SVG và ảnh hỏng | passed |
| Chặn javascript URL, URL 404 và URL trang HTML | passed |
| URL treo: khóa lúc tải, timeout 15 giây rồi mở lại editor | passed |
| Tải/thay/xóa nền bằng file và URL | passed |
| Ảnh đúng 200 KB và URL HTTP thật từ server được nhận | passed |
| Zalo/WhatsApp số điện thoại, mạng xã hội và ẩn khi trống | passed |
| Validation link/tiêu đề, giữ dữ liệu khi lưu bị chặn | passed |
| Lưu/reload giữ mọi trường, ảnh và thứ tự | passed |
| Desktop/mobile preview và màn hình hẹp không tràn ngang | passed |
| Xuất bản chưa có API: nút bị khóa | passed |
| Khôi phục mặc định chỉ đổi form; lưu xong mới thay bản đã lưu | passed |
| Không có lỗi JavaScript trong các thao tác | passed |
| Đọc cấu hình ảnh cũ không có tiêu đề/chữ nút | passed |
| JSON cấu hình hỏng vẫn mở form mặc định | passed |
| Bộ nhớ quota: báo lỗi, giữ form và bản trong phiên | passed |
| Bộ nhớ blocked: báo lỗi, giữ form và bản trong phiên | passed |
| Cấu hình được tách riêng theo khách sau khi đổi phiên | passed |
