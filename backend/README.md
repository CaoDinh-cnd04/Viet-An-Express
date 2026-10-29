# Việt An Express — Backend (.NET 10)

ASP.NET Core Web API, kiến trúc **modular monolith**: mỗi module là 1 project, dùng chung database SQL Server `vietanexpress`.

> **Portal không tự tạo bảng.** Dữ liệu đọc / ghi thẳng các bảng có sẵn của hệ thống cũ (`dbo.TCustomer`, `dbo.MaVanDon`, `dbo.MaVanDon_PCS_DIM`, `dbo.MaVanDon_ChiTietHang`). Đơn nháp & chưa in cũng nằm trong `dbo.MaVanDon` (`Status = 0`). Chức năng mới cần bảng thì hỏi trước.

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

Yêu cầu: .NET SDK 10, SQL Server (máy dev: `LAPTOP-K91T0OHE\SQLEXPRESS01`, database `vietanexpress`), `dotnet tool install -g dotnet-ef`.

```powershell
cd backend/src/VietAnExpress.API

# Bí mật KHÔNG để trong appsettings — dùng user-secrets khi dev
dotnet user-secrets set "ConnectionStrings:Default" "Server=LAPTOP-K91T0OHE\SQLEXPRESS01;Database=vietanexpress;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "Jwt:Secret" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"

dotnet run --launch-profile http     # http://localhost:3000/swagger
```

Khi chạy ở môi trường Development, API tự áp migration các module (`Database:MigrateOnStartup`) — tạo migration xong phải viết xong nội dung trước khi khởi động lại API. Đăng nhập bằng tài khoản khách có sẵn trong `dbo.TCustomer` (`Login_UserName` / `Login_Password`).

Frontend (`web/`, `npm run dev`) proxy `/api` sang `http://localhost:3000`, nên chạy cả hai là dùng được ngay.

**Deploy:** đặt biến môi trường `ConnectionStrings__Default`, `Jwt__Secret`, `Cors__AllowedOrigins__0`… Không commit bí mật vào repo.

## 2. Cấu trúc

```
backend/
├── src/
│   ├── VietAnExpress.API/                         Host: Program.cs (ghép nối), Startup/ (đăng ký module, Web API, pipeline HTTP), Infrastructure/ (email, lỗi toàn cục, Swagger)
│   ├── BuildingBlocks/VietAnExpress.SharedKernel/
│   │   ├── Domain/        BaseEntity (audit, xoá mềm, BranchId), AggregateRoot, IDomainEvent, IIntegrationEvent
│   │   ├── Results/       Result<T>, Error, PagedResult<T>
│   │   ├── Exceptions/    DomainException (422), ValidationException (400), NotFound, Conflict…
│   │   ├── Application/   ICurrentUser, ValidationBehavior, LoggingBehavior, VietnamTime
│   │   ├── Authorization/ HasPermission, PermissionPolicyProvider, IPermissionProvider, claim / role
│   │   ├── Persistence/   AuditableEntityInterceptor, DomainEventsInterceptor, AddModuleDbContext, sequence
│   │   └── Web/           ApiControllerBase, ApiResponse, ProblemDetailsMapper
│   └── Modules/
│       ├── Identity/   VietAnExpress.Identity (+ .Contracts)    đăng nhập, tài khoản con, MyTracking, thông báo, hỗ trợ & góp ý, /admin
│       ├── Customers/  VietAnExpress.Customers (+ .Contracts)   hồ sơ khách dbo.TCustomer, nhân viên dbo.USERS (chỉ đọc)
│       ├── Shipments/  VietAnExpress.Shipments (+ .Contracts)   dbo.MaVanDon (+ 2 bảng chi tiết; đơn chưa in Status = 0)
│       └── Ecommerce/  VietAnExpress.Ecommerce (+ .Contracts)   kênh bán, shop đã kết nối, đơn e-com: dbo.KenhTMDT, dbo.KetNoiTMDT, dbo.DonTMDT
├── tests/  Identity.Tests, Shipments.Tests, Ecommerce.Tests, Architecture.Tests
└── scripts/ add-migration.ps1, update-database.ps1, publish-ftp.ps1, iis/, legacy-tables.sql, setup-portal-current.sql…
```

### Trong module: mỗi thư mục = 1 chức năng / nhóm bảng

Mỗi thư mục chứa đủ controller, use case, DTO, lỗi, entity và cấu hình EF của chức năng đó — sửa 1 chức năng chỉ mở 1 thư mục.

```
VietAnExpress.Identity/
├── Auth/           đăng nhập, phiên, JWT — dbo.TCustomer (CustomerLogin)
├── Staff/          tài khoản con — dbo.TCustomer_NguoiLienLac
├── MyTracking/     dbo.TCustomer_MyTracking + dbo.TCustomer_MyTracking_Banner
├── Notifications/  dbo.TThongBao + dbo.TThongBao_DaDoc (+ dbo.TCustomerType chỉ đọc)
├── Support/        hỗ trợ & góp ý (SupportRequest), lời nhắn trang chủ — dbo.TContact
├── Admin/          đăng nhập trang /admin (thông báo / hỗ trợ của /admin nằm ở Notifications/, Support/)
├── Shared/         IdentityDbContext, IdentityErrors (lỗi chung), mẫu email
└── Migrations/

VietAnExpress.Shipments/
├── OrderData/      bảng dbo.MaVanDon + MaVanDon_PCS_DIM + MaVanDon_ChiTietHang: entity, cấp số bill, dựng / ghi đơn (dùng chung)
├── Orders/         "Đơn hàng của tôi": danh sách, chi tiết, hành trình, copy đơn
├── Drafts/         đơn nháp & chưa in, In & cấp bill (PrintDraft)
├── Printing/       in chứng từ (bill A4, invoice, CVCK, nhãn A6), xuất bảng kê Excel
├── Import/         tạo đơn từ Excel, đọc file invoice
├── AddressBook/    người gửi / người nhận đã dùng
├── Catalog/        nhóm hàng (dbo.NhomHangHoa), thư viện mặt hàng (dbo.MatHangKhachHang), tra mã HS (dbo.Catalog_HsCode)
├── Geo/            nước, mã bưu chính, gợi ý địa chỉ, vùng sâu vùng xa (dbo.VungXauVungXa)
├── Tracking/       tra cứu công khai, HUB-EZ
├── Shared/         ShipmentsDbContext, ShipmentsApi (Contracts), quy tắc cân, nước EU
└── Migrations/

VietAnExpress.Ecommerce/
├── Stores/         kênh bán (dbo.KenhTMDT) + shop đã kết nối (dbo.KetNoiTMDT)
├── Orders/         đơn e-com (dbo.DonTMDT): xem, nhập tay, sửa, xác nhận, xóa
├── Shared/         EcommerceDbContext
└── Migrations/

VietAnExpress.Customers/
├── Profiles/       hồ sơ khách dbo.TCustomer
├── VietAnUsers/    nhân viên Việt An dbo.USERS (nhân viên phụ trách khách)
└── Shared/         CustomersDbContext, CustomersApi (Contracts)
```

### Quy ước đặt file

- **Namespace = thư mục:** `Shipments/Drafts/SaveDraft.cs` → `namespace VietAnExpress.Shipments.Drafts`. EF so model theo bảng / cột nên đổi namespace / tên kiểu không sinh migration (test `MigrationSnapshotTests` kiểm tra).
- **Mỗi use case 1 file, đặt tên theo hành động:** `SaveDraft.cs` = `SaveDraftCommand` + `SaveDraftHandler`; `GetOrders.cs` = `GetOrdersQuery` + `GetOrdersHandler`. Không gom nhiều handler vào 1 class. Body request chỉ use case đó dùng thì đặt luôn trong file use case.
- **Phần dùng chung của nhiều use case** tách lớp riêng, tên nói rõ việc: `DraftStore` (đọc / khóa / xoá nháp), `DraftOrders` (hàm thuần dựng / kiểm tra nháp), `StaffAccounts`, `CustomerInbox`, `AdminNotificationMapper`, `OrderLookup`…
- **Mỗi chức năng:** `<X>Controller.cs` (controller quản trị: `Admin<X>Controller.cs`), `<X>Dtos.cs`, `<X>Errors.cs`, `<X>Rules.cs` (hàm thuần, có test), `<X>Validators.cs`, entity + `<Entity>Configuration.cs`.
- Lỗi chung cả module (`IdentityErrors.NotLoggedIn`, `AdminOnly`) ở `Shared/`; lỗi riêng ở thư mục chức năng (`AuthErrors`, `StaffErrors`, `MyTrackingErrors`…).

## 3. Quy tắc bắt buộc (có test tự kiểm tra)

1. Module chỉ tham chiếu **SharedKernel** và **Contracts** của module khác. `Architecture.Tests` sẽ fail nếu vi phạm.
2. Mọi class trong module là `internal`; chỉ `<Module>Module` là `public`. Contracts là `public`.
3. **Không tạo bảng mới.** Bảng của hệ thống cũ map bằng `ToTable(..., t => t.ExcludeFromMigrations())`; module chỉ dùng bảng cũ đăng ký DbContext bằng `AddLegacyDbContext` (không migration, không bảng lịch sử). Mã khách ở mọi nơi là `dbo.TCustomer.CustomerID` (`long`); cần hồ sơ khách thì gọi `ICustomersApi`.
4. Giao tiếp giữa các module: đồng bộ, qua interface trong Contracts.
5. Entity kế thừa `BaseEntity`: interceptor tự điền `CreatedAt`, `UpdatedAt`; lệnh xoá được đổi thành xoá mềm.
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

**Thêm use case:** tạo file `<ThưMụcChứcNăng>/<HànhĐộng>.cs` (record command / query + handler riêng), validator thêm vào `<X>Validators.cs` cùng thư mục, rồi thêm 1 action trong controller của chức năng.

**Thêm module mới:** tạo 2 project `VietAnExpress.X` và `VietAnExpress.X.Contracts` (copy csproj từ module có sẵn). Viết `XModule.AddXModule()`, thêm vào `src/VietAnExpress.API/Startup/ModuleRegistration.cs` (1 dòng `AddXModule` và thêm assembly vào mảng `ModuleAssemblies`), rồi bổ sung tên module vào `Architecture.Tests` và 2 script.

### Quy trình migration (bảng portal tự quản lý)

Máy chủ đã áp mọi migration hiện có nên **không gộp / sửa / xoá migration cũ**. Mỗi thay đổi bảng đi theo thứ tự:

1. Chốt thiết kế bảng / cột với người dùng trước (không tạo bảng mới khi chưa được duyệt), tránh tạo rồi lại bỏ.
2. Sửa entity / `Configuration`, chạy `./scripts/add-migration.ps1 -Module X -Name TenRoRang`, đọc lại file migration sinh ra.
3. `dotnet test` — `MigrationSnapshotTests` (mỗi module có migration) báo lỗi nếu model lệch snapshot (đổi entity mà quên tạo migration).
4. Xuất SQL cho máy chủ (`dotnet ef migrations script <migration trước> <migration mới> --idempotent`) lưu vào `publish/migrations/`, gửi dev hệ thống chính chạy **trước** khi đẩy backend — portal không tự áp lên máy chủ.

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

## Deploy: hosting Windows IIS qua FTP

Giao diện và API chạy chung một tên miền: site IIS trỏ vào thư mục `frontend` (React build tĩnh), thư mục `backend` là IIS Application `/api` — cookie đăng nhập `SameSite=Strict` hoạt động không cần proxy.

1. Build: `powershell -File backend/scripts/publish-ftp.ps1` → `publish/frontend`, `publish/backend`, `publish/app_offline.htm`.
2. Đẩy qua FTP: chép đè `frontend`; với `backend` chép `app_offline.htm` vào trước, chép đè file, xong thì xóa `app_offline.htm`. Không xóa `appsettings.Production.json` và `logs` trên máy chủ.
3. Cấu hình máy chủ (1 lần, người quản trị): [scripts/iis/HUONG_DAN_IIS.md](scripts/iis/HUONG_DAN_IIS.md) — .NET 10 Hosting Bundle, URL Rewrite, SSL, `appsettings.Production.json` (connection string `vietanexpress`, `Jwt:Secret`).
4. Database mới: nạp bảng hệ thống cũ, chạy `scripts/legacy-tables.sql` rồi `scripts/setup-portal-current.sql` ([cách chạy](docs/PORTAL_SETUP_SQL.md)).

Không commit connection string hoặc JWT secret.

## 6. Dữ liệu dùng chung với hệ thống cũ

Hệ thống cũ (kho, vận hành) vẫn chạy song song trên cùng database. Portal dùng thẳng các bảng đó:

| Việc | Bảng |
|---|---|
| Đăng nhập, đổi mật khẩu | `dbo.TCustomer` (`Login_UserName`, `Login_Password`) |
| Hồ sơ khách (tên công ty, người liên hệ, SĐT, email) | `dbo.TCustomer` (chỉ đọc) |
| Đơn hàng, tra cứu công khai | `dbo.MaVanDon` |
| Chi tiết kiện / dòng hàng invoice | `dbo.MaVanDon_PCS_DIM`, `dbo.MaVanDon_ChiTietHang` |
| Đơn nháp & chưa in | `dbo.MaVanDon` với `Status = 0` (+ 2 bảng chi tiết) |
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
  - Giá trị ghi theo dữ liệu cũ đang có: `Service = 1`, `Status = 1`, `Dich_Vu` dạng `DHL|Singapore`, `ConsigneeEmail = ''` (cột NOT NULL). `SenderCountryID = 231` (Việt Nam); `ConsigneeCountryID` tra từ `dbo.Countries.CountryID` theo `CountryCode`, nháp cũ thiếu mã thì khớp `CountryName`. Đọc danh mục một lần cho mỗi lần cấp bill / lô import; không tìm thấy hoặc trùng danh mục thì giữ NULL. Bảng `Countries` chỉ đọc, không thuộc model / migration của portal.

**Bảng cũ của portal đã bỏ:** các schema `identity`, `customers` và bảng `shipments.Shipments*` do phiên bản trước tự sinh (migration `DropShipmentsKeepDrafts` + `scripts/drop-portal-tables.sql`). Máy khác còn các bảng này: chạy `update-database.ps1` trước, rồi mới chạy script SQL.

## 7. Còn thiếu so với frontend (`web/docs/API_CONTRACT.md`)

Các endpoint `/pickups`, `/pricing`, `/ecommerce`, `/troubles`, `/notifications`… chưa có vì database chưa có bảng tương ứng — frontend hiện trạng thái "đang cập nhật". Khi có bảng, làm tiếp theo đúng hợp đồng trong `web/docs/API_CONTRACT.md`.

## MyTracking dùng bảng hệ thống cũ

Cấu hình và ảnh lưu trong [dbo.TCustomer_MyTracking và dbo.TCustomer_MyTracking_Banner](docs/MYTRACKING_STORAGE.md). Migration chuyển dữ liệu giữ đường dẫn, trạng thái xuất bản và ảnh, rồi xóa bảng MyTrackingCauHinh. API giữ nguyên cấu trúc.
