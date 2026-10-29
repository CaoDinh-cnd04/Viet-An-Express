# Cấu trúc thư mục & cách thêm / sửa chức năng

Đọc file này trước khi tạo chức năng mới hoặc sửa chức năng cũ. Mục tiêu: mỗi chức năng nằm gọn trong 1 thư mục, sửa chỗ này không ảnh hưởng chỗ khác.
Chi tiết hơn: [backend/README.md](backend/README.md) (mục 2–4), [web/README.md](web/README.md) (mục Cấu trúc, Quy ước code).

---

## 1. Backend (`backend/src/`)

```
VietAnExpress.API/            Host — Program.cs chỉ ghép nối
  Startup/                    ModuleRegistration (thêm module ở đây), WebApiSetup, HttpPipeline, HostSetup, StartupTasks
  Infrastructure/             email, lỗi toàn cục, Swagger, người dùng hiện tại
BuildingBlocks/VietAnExpress.SharedKernel/   Result, Error, ApiControllerBase, phân quyền, persistence dùng chung
Modules/<Module>/VietAnExpress.<Module>/     mỗi thư mục con = 1 chức năng / nhóm bảng
Modules/<Module>/VietAnExpress.<Module>.Contracts/   interface cho module khác gọi (public)
```

| Module | Thư mục chức năng |
|---|---|
| Identity | `Auth/` (đăng nhập, phiên — dbo.TCustomer) · `Staff/` (tài khoản con — TCustomer_NguoiLienLac) · `MyTracking/` · `Notifications/` (TThongBao + TThongBao_DaDoc) · `Support/` (hỗ trợ & góp ý — TContact) · `Admin/` (đăng nhập /admin) · `Shared/` (DbContext, IdentityErrors, mẫu email) · `Migrations/` |
| Shipments | `OrderData/` (bảng MaVanDon + 2 bảng chi tiết, cấp số bill — dùng chung) · `Orders/` · `Drafts/` · `Printing/` · `Import/` · `AddressBook/` · `Catalog/` · `Geo/` · `Tracking/` · `Shared/` · `Migrations/` |
| Ecommerce | `Stores/` · `Orders/` · `Shared/` · `Migrations/` |
| Customers | `Profiles/` (TCustomer) · `VietAnUsers/` (USERS) · `Shared/` |

### Trong 1 thư mục chức năng (vd `Shipments/Drafts/`)

| File | Nội dung |
|---|---|
| `<HanhDong>.cs` — vd `SaveDraft.cs`, `GetDrafts.cs` | **1 use case / file**: record `…Command` / `…Query` + class `…Handler` riêng |
| `<X>Controller.cs` | endpoint của chức năng (trang quản trị: `Admin<X>Controller.cs`) |
| `<X>Dtos.cs` | dữ liệu trả về / gửi lên |
| `<X>Errors.cs` | lỗi riêng của chức năng (lỗi chung module ở `Shared/<Module>Errors.cs`) |
| `<X>Rules.cs` | hàm thuần (tính toán, kiểm tra) — **có test** |
| `<X>Validators.cs` | FluentValidation cho command |
| `<Entity>.cs` + `<Entity>Configuration.cs` | entity EF và cách map bảng / cột |
| lớp dùng chung tên rõ việc | vd `DraftStore` (đọc / khóa DB), `StaffAccounts`, `OrderLookup` — khi ≥ 2 use case cần cùng 1 đoạn |

### Quy tắc
- **Namespace = thư mục**: `Shipments/Drafts/SaveDraft.cs` → `namespace VietAnExpress.Shipments.Drafts;`
- **Không gom nhiều handler vào 1 class**, không viết hàm dùng chung dạng `private` lặp lại ở nhiều handler.
- Module chỉ gọi module khác qua **`.Contracts`** (vd `ICustomersApi`, `IShipmentsApi`). `Architecture.Tests` chặn vi phạm.
- Mọi class `internal`, chỉ `<Module>Module.cs` là `public`.
- **Không tạo bảng mới** khi chưa hỏi. Bảng hệ thống cũ map với `.LegacyTable()` (không migration).

### Thêm chức năng mới (backend)
1. Chọn đúng thư mục chức năng (chưa có thì tạo thư mục mới trong module phù hợp).
2. Tạo `<HanhDong>.cs` (command/query + handler), thêm validator vào `<X>Validators.cs`, thêm action vào controller.
3. Đổi bảng / cột (đã được duyệt): sửa entity + Configuration → `./backend/scripts/add-migration.ps1 -Module X -Name TenRoRang` → xuất SQL gửi dev chạy trên máy chủ **trước** khi đẩy backend. Không sửa / xóa migration cũ.
4. Viết test cho phần `Rules` / handler; chạy `dotnet test --solution VietAnExpress.slnx` (gồm `MigrationSnapshotTests` báo nếu quên tạo migration).
5. Thêm endpoint mới → cập nhật `web/docs/API_CONTRACT.md`.

---

## 2. Web (`web/src/`)

```
app/            router (routes.tsx), layout, menu (layout/nav.config.ts) — được import trang của feature
features/<x>/   mỗi chức năng 1 thư mục, chỉ dùng nhau qua index.ts
  api.ts        hook gọi API (nhiều nhóm thì thành thư mục api/ + index.ts) — nơi DUY NHẤT gọi http
  types.ts · constants.ts · schema.ts (form)
  lib/          hàm thuần + *.test.ts
  hooks/        logic trạng thái (vd useWizardSteps, useOrderSubmit)
  components/   component nhỏ, mỗi file 1 việc (+ .module.css cùng tên)
  pages/        trang — chỉ ghép component, không chứa logic dài
  index.ts      cổng công khai cho feature khác import
shared/         dùng chung, KHÔNG import features/
  api/ · config/ · model/ (kiểu nghiệp vụ dùng chung) · lib/ · ui/ · styles/tokens.css · i18n/
architecture.test.ts   kiểm tra ranh giới (chạy cùng npm test)
```

Feature hiện có: `account` `admin` `auth` `create-order` `drafts` `ecommerce` `landing` `mytracking` `notifications` `onboarding`
`order-form` (feature nền: schema / quy tắc form đơn hàng, không có trang) `order-import` `orders` `pricing` `staff` `support` `terms`.

### Quy tắc (sai là test / lint đỏ)
- Feature dùng feature khác **chỉ qua `@/features/<x>`** — không import file bên trong, không vòng phụ thuộc (A → B → A).
- 2 feature cùng cần 1 thứ → đưa xuống **feature nền** (như `order-form`) hoặc **`shared/`** (`shared/model/` cho kiểu dùng chung).
- `shared/` không import `features/` / `app/`.
- Màu / cỡ chữ / khoảng cách dùng biến trong `shared/styles/tokens.css`; giao diện gọn kiểu phần mềm nghiệp vụ (bảng, form nhãn trái), không gradient / thẻ to.
- Chữ hiển thị viết tiếng Việt trong `t('…')` và **thêm bản dịch** vào `shared/i18n/en/<khu-vuc>.ts`. Bỏ chữ nào thì xóa bản dịch (test báo khoá thừa). Chữ đến từ dữ liệu lúc chạy → `shared/i18n/runtime-texts.ts`.

### Thêm trang / chức năng mới (web)
1. Tạo `features/<ten>/` theo cấu trúc trên; trang `pages/<Ten>Page.tsx` (default export).
2. Khai báo route trong `app/routes.tsx`, menu trong `app/layout/nav.config.ts`.
3. Gọi API trong `api.ts`; endpoint chưa có → `isNotImplemented()`, không dữ liệu giả.
4. Tính toán viết vào `lib/` + test; logic trạng thái dài tách hook trong `hooks/`.
5. Feature khác cần dùng → export qua `index.ts`.
6. Chạy trong `web/`: `npm run typecheck` · `npm run lint` · `npm test` · `npm run build`.

---

## 3. Build & đẩy lên máy chủ
1. **Đóng Total Commander / Explorer đang mở thư mục `publish/`** (đang mở thì script không xóa được).
2. `.\backend\scripts\publish-ftp.ps1` (PowerShell, tại thư mục repo) → `publish/frontend`, `publish/backend`.
   Script **xóa cả `publish/`** — file SQL trong `publish/migrations/` cần giữ thì sao lưu trước rồi chép lại.
3. Kiểm tra `publish/frontend/index.html` mang ngày giờ vừa build.
4. FTP: backend — đặt `app_offline.htm` → chép đè `backend/` → xóa `app_offline.htm`; frontend — chép đè **cả thư mục `assets`**. Mở trang, Ctrl+F5.
5. Có migration mới → gửi file SQL cho dev chạy trên máy chủ **trước** khi đẩy backend. `publish/` và `backend/scripts/` có mật khẩu máy chủ — không đưa lên git.
