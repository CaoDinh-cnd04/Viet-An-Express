# IntegrityProbe

Công cụ chẩn đoán tình huống ghi đồng thời bằng ánh xạ EF hiện tại. Chỉ dùng cho kiểm thử, không đưa vào bản publish của API.

Đặt `VIETAN_TEST_SQLSERVER` thành kết nối máy SQL Server cho phép tạo/xóa database kiểm thử. Công cụ tạo tên `vietan_tests_integrity_<GUID>`, tạo schema kiểm thử từ cấu hình EF, tái hiện các thao tác xen kẽ, rồi xóa đúng database do nó tạo trong `finally`. Không đọc/ghi bảng nghiệp vụ của database ứng dụng. Các truy vấn so sánh schema chỉ đọc file JSON do script audit xuất ra.

Chạy từ `backend/`:

```powershell
$env:VIETAN_TEST_SQLSERVER='Server=<server>;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet run --project tools/IntegrityProbe/IntegrityProbe.csproj -- docs/write-conflicts-2026-10-15.json docs/integrity-audit-2026-10-15.json
```

Đầu ra `PROBE` là bằng chứng hành vi hiện tại; **exit code 0 chỉ có nghĩa công cụ chạy xong**, không có nghĩa ứng dụng không còn lỗi. Đây không phải bộ test khẳng định hành vi hiện tại là đúng. Xem báo cáo kiểm tra để biết kết quả nào là lỗi cần sửa.

Các thao tác xen kẽ giữ hai bản entity đã đọc trước cùng lúc để tái hiện request dùng dữ liệu cũ, rồi thực thi `SaveChangesAsync` của EF trên SQL Server thật. Chúng kiểm tra lớp lưu trữ và ánh xạ; không gọi HTTP handler hoặc gửi email. Giá trị `HttpStatus` được kiểm tra bằng `ProblemDetailsMapper` của ứng dụng, không phải response HTTP từ thao tác ghi thật.

Kiểm tra cấp số chạy 20 transaction đồng thời, mỗi transaction đọc `MAX(OrderNumber)` bằng đúng lock hint của bộ cấp số hiện tại, ghi một đơn riêng rồi commit. Kết quả này chỉ áp dụng các bên cùng tuân thủ giao thức khóa đó; không xác minh cách phần mềm cũ cấp số.

Sau khi sửa, cần thêm regression test vào các project test chính, kiểm tra kết quả mong muốn và xác minh lại qua HTTP trên tài khoản/database kiểm thử.

Chế độ `--export-portal-ddl <thư-mục>` chỉ xuất DDL cuối của 3 module, giữ `ExcludeFromMigrations` cho các bảng đã nạp, không kết nối database. Được dùng để sinh [file SQL dựng portal hiện hành](../../docs/PORTAL_SETUP_SQL.md).
