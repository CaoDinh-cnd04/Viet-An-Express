# Cấu hình IIS cho Việt An Express Portal

Tài liệu cho người quản trị máy chủ. Chỉ cần làm một lần; các lần cập nhật sau người phát triển tự đẩy file qua FTP.

## Bố cục

Hai site IIS, hai tên miền con của `vietanexpress.com.vn`:

| Site | Tên miền | Thư mục | Nội dung |
|---|---|---|---|
| Giao diện | `beta.vietanexpress.com.vn` | `frontend\` | React (file tĩnh) |
| API | `api.vietanexpress.com.vn` | `backend\` | ASP.NET Core (.NET 10), route `/api/v1/...`, kiểm tra `/health` |

Giao diện gọi `http://api.vietanexpress.com.vn/api/v1/...` (ghi vào bản build — `publish-ftp.ps1 -ApiBaseUrl`).
Backend cho phép tên miền giao diện qua `Cors__AllowedOrigins__0` trong `backend\web.config`. Hai tên miền con cùng
`vietanexpress.com.vn` nên cookie đăng nhập (`SameSite=Strict`) vẫn gửi kèm. Đổi tên miền giao diện / API thì báo bên phát triển
để sửa hai chỗ này.

Cách cũ vẫn chạy được: 1 site, backend là IIS Application `/api` cùng tên miền — khi đó build với `-ApiBaseUrl /api/v1`.

## 1. Cài đặt trên máy chủ

1. **ASP.NET Core Hosting Bundle** (để có module `AspNetCoreModuleV2` cho IIS; khuyến nghị bản 10.0: https://dotnet.microsoft.com/download/dotnet/10.0). Cài xong chạy `iisreset`. Backend đã đóng gói sẵn runtime .NET 10 (self-contained, win-x64) nên máy chủ **không cần** cài thêm .NET 10 Runtime; máy chủ phải là Windows 64-bit.
2. **IIS URL Rewrite 2.1** (https://www.iis.net/downloads/microsoft/url-rewrite) — cho `frontend\web.config`.

## 2. Site giao diện — `beta.vietanexpress.com.vn`

1. Tạo site trỏ tới thư mục `frontend\`, binding tên miền `beta.vietanexpress.com.vn`.
2. Application Pool: *No Managed Code*.

## 3. Site API — `api.vietanexpress.com.vn`

1. **DNS:** tạo bản ghi `api.vietanexpress.com.vn` trỏ về IP máy chủ (như `beta`).
2. Tạo site **riêng** trỏ tới thư mục `backend\`, binding tên miền `api.vietanexpress.com.vn` (cổng 80; có SSL thì thêm 443).
3. Application Pool riêng: *.NET CLR version = No Managed Code*, *Managed pipeline = Integrated*.
4. Cấp quyền **Modify** cho tài khoản pool (`IIS AppPool\<tên pool>`) trên `backend\logs\` (tạo thư mục nếu chưa có).
5. Kiểm tra: `http://api.vietanexpress.com.vn/health` → `Healthy`; `http://api.vietanexpress.com.vn/api/v1/auth/session` → JSON.

**SSL:** khi có chứng chỉ cho cả hai tên miền, đổi sang `https://` ở cả hai đầu (build lại giao diện với `-ApiBaseUrl https://api.../api/v1`,
sửa `Cors__AllowedOrigins__0`, `Company__PortalUrl`, bỏ `Jwt__SecureCookies=false` trong `backend\web.config`).

## 4. Cấu hình bí mật — `backend\appsettings.Production.json`

Tạo file này **trực tiếp trên máy chủ** (không nằm trong bản đẩy FTP, nên các lần cập nhật không ghi đè). Không gửi nội dung file qua chat / email.

```json
{
  "ConnectionStrings": {
    "Default": "Server=<sql-server>;Database=vietanexpress;User ID=<sql-user>;Password=<sql-password>;Encrypt=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Secret": "<chuỗi ngẫu nhiên tối thiểu 32 ký tự>"
  },
  "Company": {
    "PortalUrl": "https://<tên-miền>"
  },
  "Email": {
    "UserName": "<gmail gửi thông báo>",
    "Password": "<mật khẩu ứng dụng Gmail>",
    "InboxAddress": "<email nhận góp ý>"
  }
}
```

- Tạo `Jwt:Secret`, ví dụ trong PowerShell: `[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])`. Đổi secret thì mọi người dùng phải đăng nhập lại.
- Nếu SQL Server cùng máy và dùng Windows Authentication: `Server=.\SQLEXPRESS;Database=vietanexpress;Trusted_Connection=True;TrustServerCertificate=True`, và cấp quyền đọc/ghi database cho `IIS AppPool\<tên pool>`.
- Mục `Email` để trống thì portal vẫn chạy, chỉ không gửi email.
- Cách khác (đang dùng): chuỗi kết nối và `Jwt__Secret` đặt sẵn trong `backend\web.config` (thẻ `<environmentVariables>`) — bản đẩy FTP đã có, lấy từ file cục bộ `backend/scripts/iis/backend.web.config` (không lên git). Biến môi trường ghi đè `appsettings.Production.json`. Ghi đè `web.config` trên máy chủ thì IIS tự khởi động lại backend.
- Tùy chọn: `"GeoNames": { "Username": "<tài khoản geonames.org>" }` để tra mã bưu chính.

Môi trường mặc định trên IIS là `Production` nên file này được đọc tự động.

## 5. Database

Database `vietanexpress` cần có sẵn các bảng hệ thống cũ (`dbo.MaVanDon`, `dbo.MaVanDon_ChiTietHang`, `dbo.MaVanDon_PCS_DIM`, `dbo.TCustomer`, `dbo.USERS`, `dbo.VungXauVungXa`, `dbo.Catalog_HsCode`; script `backend/scripts/legacy-tables.sql` tạo thêm `dbo.TCustomer_NguoiLienLac`, `dbo.TContact`, `dbo.TCustomer_MyTracking`, `dbo.TCustomer_MyTracking_Banner`, `dbo.Countries`). Khi dựng mới sau khi nạp các bảng này, chạy **`backend/scripts/setup-portal-current.sql`**: file gộp chỉ tạo bảng portal hiện hành và bổ sung cấu trúc trên 4 bảng cũ có thay đổi. Cách chạy và điều kiện: `backend/docs/PORTAL_SETUP_SQL.md`.

Database đã chạy portal đời trước: dùng `update-database.ps1` để thực hiện các bước chuyển dữ liệu; không dùng file baseline để bỏ qua migration. Sao lưu trước khi áp cấu trúc.

## 6. Kiểm tra

| Mở | Kết quả đúng |
|---|---|
| `https://<tên-miền>/` | Trang chủ portal |
| `https://<tên-miền>/orders/new` (mở trực tiếp / F5) | Trang tạo đơn, không lỗi 404 |
| `http://api.vietanexpress.com.vn/health` | `Healthy` (HTTP 200) — có kết nối database |
| Đăng nhập bằng tài khoản khách thật | Vào được trang tổng quan |

## Xử lý lỗi

| Hiện tượng | Nguyên nhân thường gặp |
|---|---|
| `HTTP Error 500.19` ở `/` | Chưa cài URL Rewrite |
| `HTTP Error 500.19` / `500.0` ở site API | Chưa cài Hosting Bundle, hoặc chưa `iisreset` sau khi cài |
| `HTTP Error 500.30` ở site API | Backend lỗi khi khởi động: thiếu/sai `appsettings.Production.json`, `Jwt:Secret` ngắn hơn 32 ký tự, không kết nối được SQL. Xem `backend\logs\` |
| `http://api.vietanexpress.com.vn/health` trả 503 | Không kết nối được database |
| Trang mở được nhưng đăng nhập không giữ phiên | Đang dùng `http://` thay vì `https://` |

Cần xem lỗi chi tiết khi khởi động: tạm đặt `stdoutLogEnabled="true"` trong `backend\web.config`, xem `backend\logs\stdout_*.log`, xong thì tắt lại.

## Cập nhật phiên bản (bên phát triển làm qua FTP)

- **Frontend:** ghi đè nội dung `frontend\`.
- **Backend:** chép `app_offline.htm` vào `backend\` (IIS dừng app, nhả file `.dll`) → ghi đè nội dung `backend\` → xóa `app_offline.htm`.
- Không ghi đè / xóa `backend\appsettings.Production.json` và `backend\logs\`.
