# Việt An Express — Backend (.NET 10)

ASP.NET Core Web API, kiến trúc **modular monolith**: mỗi module là 1 project, dùng chung database SQL Server `vietan_app`.

> **Portal không tự tạo bảng.** Dữ liệu đọc / ghi thẳng các bảng có sẵn của hệ thống cũ (`dbo.TCustomer`, `dbo.MaVanDon`, `dbo.MaVanDon_PCS_DIM`, `dbo.MaVanDon_ChiTietHang`). Ngoại lệ duy nhất đang tạm giữ: `shipments.OrderDrafts` (đơn nháp). Chức năng mới cần bảng thì hỏi trước.

| Thành phần | Công nghệ |
|---|---|
| Nền tảng | .NET 10 (LTS), ASP.NET Core Web API |
| Dữ liệu | EF Core 10 + SQL Server |
| Ứng dụng | MediatR 12.5 (command/query), FluentValidation 12 |
| Bảo mật | JWT (access + refresh tự chứa, không lưu DB), phân quyền theo permission |
| Vận hành | Serilog, Swagger, Asp.Versioning (`/api/v1/...`), health check `/health` |
| Test | xUnit v3 + Moq |

> MediatR ghim ở 12.5.0 — bản cuối cùng giấy phép Apache-2.0. Từ 13.x MediatR cần license thương mại.

## 1. Chạy lần đầu

Yêu cầu: .NET SDK 10, SQL Server (máy dev: `LAPTOP-K91T0OHE\SQLEXPRESS01`, database `vietan_app`), `dotnet tool install -g dotnet-ef`.

```powershell
cd backend/src/VietAnExpress.API

# Bí mật KHÔNG để trong appsettings — dùng user-secrets khi dev
dotnet user-secrets set "ConnectionStrings:Default" "Server=LAPTOP-K91T0OHE\SQLEXPRESS01;Database=vietan_app;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "Jwt:Secret" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"

dotnet run --launch-profile http     # http://localhost:3000/swagger
```

Khi chạy ở môi trường Development, API tự áp migration của `shipments.OrderDrafts` (`Database:MigrateOnStartup`). Đăng nhập bằng tài khoản khách có sẵn trong `dbo.TCustomer` (`Login_UserName` / `Login_Password`).

Frontend (`web/`, `npm run dev`) proxy `/api` sang `http://localhost:3000`, nên chạy cả hai là dùng được ngay.

**Deploy:** đặt biến môi trường `ConnectionStrings__Default`, `Jwt__Secret`, `Cors__AllowedOrigins__0`… Không commit bí mật vào repo.

## 2. Cấu trúc

```
backend/
├── src/
│   ├── VietAnExpress.API/                         Host: Program.cs, pipeline, Swagger, xử lý lỗi toàn cục
│   ├── BuildingBlocks/VietAnExpress.SharedKernel/
│   │   ├── Domain/        BaseEntity (audit, xoá mềm, BranchId), AggregateRoot, IDomainEvent, IIntegrationEvent
│   │   ├── Results/       Result<T>, Error, PagedResult<T>
│   │   ├── Exceptions/    DomainException (422), ValidationException (400), NotFound, Conflict…
│   │   ├── Application/   ICurrentUser, ValidationBehavior, LoggingBehavior, VietnamTime
│   │   ├── Authorization/ HasPermission, PermissionPolicyProvider, IPermissionProvider, claim / role
│   │   ├── Persistence/   AuditableEntityInterceptor, DomainEventsInterceptor, AddModuleDbContext, sequence
│   │   └── Web/           ApiControllerBase, ApiResponse, ProblemDetailsMapper
│   └── Modules/
│       ├── Identity/   VietAnExpress.Identity (+ .Contracts)    đăng nhập khách: dbo.TCustomer
│       ├── Customers/  VietAnExpress.Customers (+ .Contracts)   hồ sơ khách: dbo.TCustomer (chỉ đọc)
│       ├── Shipments/  VietAnExpress.Shipments (+ .Contracts)   dbo.MaVanDon (+ 2 bảng chi tiết), shipments.OrderDrafts
│       └── Ecommerce/  VietAnExpress.Ecommerce (+ .Contracts)   kênh bán, shop đã kết nối, đơn e-com: dbo.KenhTMDT, dbo.KetNoiTMDT, dbo.DonTMDT
├── tests/  Shipments.Tests, Identity.Tests, Architecture.Tests
└── scripts/ add-migration.ps1, update-database.ps1, drop-portal-tables.sql, install-service.ps1…
```

Bên trong mỗi module: `Domain/`, `Application/` (`Commands/`, `Queries/`, `Dtos/`, `Validators/`, `EventHandlers/`), `Infrastructure/` (`<Module>DbContext`, `Configurations/`, `Migrations/`), `Api/` (controller), `<Module>Module.cs` (đăng ký DI).

## 3. Quy tắc bắt buộc (có test tự kiểm tra)

1. Module chỉ tham chiếu **SharedKernel** và **Contracts** của module khác. `Architecture.Tests` sẽ fail nếu vi phạm.
2. Mọi class trong module là `internal`; chỉ `<Module>Module` là `public`. Contracts là `public`.
3. **Không tạo bảng mới.** Bảng của hệ thống cũ map bằng `ToTable(..., t => t.ExcludeFromMigrations())`; module chỉ dùng bảng cũ đăng ký DbContext bằng `AddLegacyDbContext` (không migration, không bảng lịch sử). Mã khách ở mọi nơi là `dbo.TCustomer.CustomerID` (`long`); cần hồ sơ khách thì gọi `ICustomersApi`.
4. Giao tiếp giữa các module: đồng bộ, qua interface trong Contracts.
5. `shipments.OrderDrafts` kế thừa `BaseEntity`: interceptor tự điền `CreatedAt`, `UpdatedAt`; lệnh xoá được đổi thành xoá mềm.
6. Controller không kiểm tra theo tên vai trò. Luôn dùng `[HasPermission(ShipmentsPermissions.Create)]`. Quyền khai báo trong code (`IPermissionProvider` của từng module, vai trò `customer`) và được cấp vào token khi đăng nhập — không lưu DB.
7. Mọi lỗi trả về dạng **ProblemDetails**, kèm `error` (mã lỗi) và `message` (câu tiếng Việt) — đúng hai trường frontend đang đọc.

## 4. Việc thường làm

```powershell
# Tạo migration (class sinh ra tự chuyển sang internal)
./scripts/add-migration.ps1 -Module Shipments -Name AddShipmentEta

# Áp migration thủ công (production, hoặc khi tắt MigrateOnStartup)
./scripts/update-database.ps1

# Test
dotnet test --solution VietAnExpress.slnx
```

Test truy vấn SQL Server: đặt `VIETAN_TEST_SQLSERVER` thành connection string của **máy chủ dành cho kiểm thử**, rồi chạy lệnh test trên. Test tự tạo database `vietan_tests_queries_<guid>` và xóa khi kết thúc; không thay đổi database ứng dụng. Tài khoản test cần quyền tạo/xóa database. CI đã cấp SQL Server riêng và biến này. Nếu chưa đặt biến, test tích hợp SQL được báo skipped; test dịch truy vấn SQL vẫn chạy.

Danh sách đơn dùng một truy vấn thống kê và một truy vấn trang (trang rỗng chỉ dùng thống kê), đọc đúng các cột giao diện cần. Test so sánh với kết quả và số đếm của truy vấn cũ, gồm lọc trạng thái, ngày, cân nặng, phân trang và phạm vi khách hàng. Invoice gần đây dùng hai truy vấn; tracking và hành trình chỉ đọc các cột cần trả về.

Kiểm tra sau deploy từ thư mục `backend/`: `./scripts/test-published.ps1 -ExpectedRevision <commit-sha>`. Script kiểm tra `/health`, trang đăng nhập, proxy Vercel, phân quyền và validation tra cứu; không tạo/sửa dữ liệu. Render trả commit đang chạy qua header `X-Vietan-Revision` của `/health`. Luồng sau đăng nhập cần tài khoản kiểm thử riêng.

**Thêm use case:** tạo file `Commands/XxxCommand.cs` (gồm record command và handler), thêm validator trong `Validators/`, rồi thêm 1 action trong controller.

**Thêm module mới:** tạo 2 project `VietAnExpress.X` và `VietAnExpress.X.Contracts` (copy csproj từ module có sẵn). Viết `XModule.AddXModule()`, thêm vào `Program.cs` (1 dòng `AddXModule` và thêm assembly vào mảng `moduleAssemblies`), rồi bổ sung tên module vào `Architecture.Tests` và 2 script.

## 5. API v1

| Module | Endpoint | Quyền |
|---|---|---|
| Tài khoản | `POST /auth/login` (`?useCookies=false` để nhận token trong body), `POST /auth/refresh`, `POST /auth/logout`, `POST /auth/change-password`, `GET /me` | đăng nhập |
| Đơn hàng (dbo.MaVanDon) | `GET /orders`, `GET /orders/{bill}`, `/events`, `POST /orders/import/preview`, `POST /orders/import`, `GET/POST/PUT/DELETE /drafts`, `POST /drafts/{id}/print` | `shipments.view/create/update/issue-bill` |
| In & bảng kê | `GET /orders/print?bills=A,B&doc=` với `bill-a4` / `invoice` / `cvck` / `label-a6` (trang HTML in, mã vạch Code128), `GET /orders/export` (.xlsx, cùng bộ lọc danh sách) | `shipments.view` |
| Công khai | `POST /public/tracking` `{ bills: string[] }` (≤ 10 mã, giới hạn 30 lần/phút/IP) | không cần |

**Xác thực:**
- Web portal dùng cookie `HttpOnly` + `SameSite=Strict`: `va_access` (JWT 15 phút) và `va_refresh` (14 ngày nếu "ghi nhớ", còn không thì 12 giờ).
- Client khác (mobile, tích hợp) gửi header `Authorization: Bearer`.
- Chỉ khách hàng đăng nhập: `Login_UserName` / `Login_Password` của `dbo.TCustomer`. Đổi mật khẩu ghi lại vào `Login_Password` **dạng chữ thường** như hệ thống cũ, để 2 hệ thống cùng đăng nhập được.
- Refresh token là JWT tự chứa (không lưu DB), mang dấu HMAC của mật khẩu hiện tại: đổi mật khẩu (trên portal hay hệ thống cũ) thì mọi phiên cũ hết hiệu lực. Đăng xuất chỉ xoá cookie.
- Chống dò mật khẩu bằng giới hạn tần suất đăng nhập theo IP (10 lần/phút).
- Frontend gặp 401 thì tự gọi `POST /auth/refresh` một lần rồi thử lại.

## Deploy: Render (backend) + Vercel (frontend)

Hướng dẫn và biến môi trường: [DEPLOYMENT.md](../DEPLOYMENT.md). Repository có `render.yaml` cấu hình Docker từ `backend/`, health check `/health`. Trên Vercel dùng Root Directory `web`, đặt `BACKEND_URL` thành URL HTTPS của dịch vụ Render và giữ API client ở `/api/v1` để cookie đăng nhập hoạt động qua proxy.

SQL Server cần truy cập được từ Render và có sẵn bảng/dữ liệu của hệ thống cũ. Không dùng connection string Windows `Trusted_Connection=True` trên Linux Render. Không commit connection string hoặc JWT secret.

## Phương án máy chủ nội bộ (qua ngrok)

```
Trình duyệt ──► Vercel (web + hàm web/api/proxy.ts) ──► https://<tên-miền>.ngrok-free.app ──► ngrok (service) ──► http://localhost:5080 (VietAnExpressApi) ──► SQL Server
```

Trình duyệt chỉ làm việc với tên miền Vercel, nên cookie đăng nhập (`SameSite=Strict`) hoạt động bình thường. Hàm proxy trên Vercel làm 2 việc: thêm header `ngrok-skip-browser-warning` để ngrok gói miễn phí không chèn trang cảnh báo, và trả 503 kèm câu tiếng Việt khi máy chủ tắt.

**Cài trên máy chủ (1 lần; mở "Windows PowerShell" bằng Run as administrator):**
```powershell
cd D:\Viet-An-Express\backend
.\scripts\install-service.ps1                            # build → C:\VietAnExpress\api, tạo service VietAnExpressApi (cổng 5080)
.\scripts\setup-ngrok.ps1 -Domain <tên-miền>.ngrok-free.app   # ngrok chạy dạng service
```
- `install-service.ps1` lấy connection string và `Jwt:Secret` từ User Secrets của máy, ghi vào `C:\VietAnExpress\api\appsettings.Production.json` (chỉ admin và service đọc được, **không** nằm trong repo). Service chạy bằng tài khoản `NT SERVICE\VietAnExpressApi`, được cấp `db_owner` trên database; tự chạy khi bật máy và tự khởi động lại khi lỗi. Log nằm ở `C:\VietAnExpress\api\logs\`.
- **Cập nhật bản mới:** `git pull` rồi chạy lại `install-service.ps1`.
- **Gỡ:** `uninstall-service.ps1` (thêm `-RemoveFiles` để xoá thư mục cài đặt).

**Trên Vercel:**
- Root Directory = `web`.
- Environment Variable `BACKEND_URL = https://<tên-miền>.ngrok-free.app`, rồi Redeploy.
- **Không** đặt `VITE_API_BASE_URL` (để mặc định `/api/v1`).

Máy chủ phải luôn bật và có mạng. Khi máy tắt, portal báo "Máy chủ Việt An đang tạm dừng".

## 6. Dữ liệu dùng chung với hệ thống cũ

Hệ thống cũ (kho, vận hành) vẫn chạy song song trên cùng database. Portal dùng thẳng các bảng đó:

| Việc | Bảng |
|---|---|
| Đăng nhập, đổi mật khẩu | `dbo.TCustomer` (`Login_UserName`, `Login_Password`) |
| Hồ sơ khách (tên công ty, người liên hệ, SĐT, email) | `dbo.TCustomer` (chỉ đọc) |
| Đơn hàng, tra cứu công khai | `dbo.MaVanDon` |
| Chi tiết kiện / dòng hàng invoice | `dbo.MaVanDon_PCS_DIM`, `dbo.MaVanDon_ChiTietHang` |
| Đơn nháp & chưa in (tạm giữ) | `shipments.OrderDrafts` |
| Kênh bán e-com (TikTok Shop, Amazon…) — bảng mới | `dbo.KenhTMDT` (danh mục, seed trong migration) |
| Shop của khách đã kết nối — bảng mới | `dbo.KetNoiTMDT` |
| Đơn e-com (kéo từ sàn; sau này cả nhập tay / Excel) — bảng mới | `dbo.DonTMDT` |

- **E-com (`dbo.KenhTMDT`, `dbo.KetNoiTMDT`):** thêm kênh mới = thêm 1 dòng `KenhTMDT` bằng migration (`Ma_Kenh` trùng `platform` của frontend) và bật `Dang_Hoat_Dong` khi đã có adapter; không đổi cấu trúc bảng. `KetNoiTMDT` giữ 1 dòng cho mỗi (khách, kênh, `Ma_Shop`); token sàn chỉ lưu đã mã hóa (`Access_Token_Ma_Hoa`, `Refresh_Token_Ma_Hoa`); `Trang_Thai` ∈ `active | expired | error | revoked`.

- **Tra cứu công khai:** tìm trong `dbo.MaVanDon` theo số VA (`OrderNumber`), mã hãng (`Bill_Connect`), `AWB` hoặc `CustomerBill`. Tên người ký nhận không hiện ra.
- **Đơn hàng (`/orders`, `/drafts`):**
  - Khách chỉ thấy / ghi dòng có `CustomerID` bằng mã khách của mình. Trạng thái suy ra từ `POD`, `POD_Est`, `Sent_Date` (xem `LegacyOrderStatus`).
  - `POST /drafts/{id}/print` cấp số vận đơn, ghi 1 dòng vào `MaVanDon` và chi tiết kiện / dòng hàng vào 2 bảng chi tiết (khoá `MaVanDonID` = `MaVanDon.ID`), trong cùng transaction với việc xoá nháp.
  - `POST /orders/import` (tạo từ file Excel mẫu) ghi giống hệt, cho các dòng hợp lệ.
  - Ở `MaVanDon_PCS_DIM`: `TrongLuong` / `QuiDoi` / `ChargeWeight` là tổng của dòng kiện; chứng từ không ghi dòng kiện. In bill / invoice đọc 2 bảng chi tiết trước, không có thì lấy form nháp.
  - **Số vận đơn** của portal lấy từ dải riêng, bắt đầu từ **90.000.001** (sequence `shipments.LegacyOrderNumberSequence`), để không trùng số hệ thống cũ cấp (hiện khoảng 6 triệu).
  - Giá trị ghi theo dữ liệu cũ đang có: `Service = 1`, `Status = 1`, `Dich_Vu` dạng `DHL|Singapore`, `ConsigneeEmail = ''` (cột NOT NULL). Chưa ghi `SenderCountryID` / `ConsigneeCountryID`, vì database chưa có bảng danh mục quốc gia.

**Bảng cũ của portal đã bỏ:** các schema `identity`, `customers` và bảng `shipments.Shipments*` do phiên bản trước tự sinh (migration `DropShipmentsKeepDrafts` + `scripts/drop-portal-tables.sql`). Máy khác còn các bảng này: chạy `update-database.ps1` trước, rồi mới chạy script SQL.

## 7. Còn thiếu so với frontend (`web/docs/API_CONTRACT.md`)

Các endpoint `/pickups`, `/pricing`, `/ecommerce`, `/troubles`, `/notifications`… chưa có vì database chưa có bảng tương ứng — frontend hiện trạng thái "đang cập nhật". Khi có bảng, làm tiếp theo đúng hợp đồng trong `web/docs/API_CONTRACT.md`.
