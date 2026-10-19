# Hợp đồng API — Việt An Express Portal (frontend `web/`)

Tài liệu này liệt kê **mọi endpoint mà frontend React đang gọi**, kèm dữ liệu gửi / nhận.
Backend mới (SQL Server) chỉ cần làm đúng các hợp đồng này là frontend chạy được, không phải sửa giao diện.

- Base URL: `/api/v1` (cấu hình bằng biến `VITE_API_BASE_URL`).
- Kiểu dữ liệu TypeScript tương ứng nằm trong `web/src/features/<feature>/types.ts` hoặc `api.ts` — đó là nguồn chuẩn.
- Trạng thái: **Có sẵn** = đã có ở backend Express cũ (đã gỡ khỏi repo) và frontend đã chạy thử với nó · **Mới** = backend cần làm thêm · **Chưa làm** = backend .NET hiện tại chưa có (gọi trả 404 — frontend hiện "Chức năng đang được kết nối máy chủ"). Kiểm tra ngày 06/10/2026.

## Quy ước chung

| Mục | Quy ước |
|---|---|
| Định dạng | JSON (`Content-Type: application/json`). Riêng `POST /troubles` là `multipart/form-data`. |
| Danh sách | `{ "success": true, "count": number, "data": T[] }` |
| Một bản ghi | `{ "success": true, "data": T }` |
| Thao tác ghi | `{ "success": true, "message": "Câu thông báo tiếng Việt" , "data"?: T }` — frontend hiện `message` lên toast. |
| Lỗi | HTTP 4xx/5xx + `{ "error": "MA_LOI", "message": "Câu báo lỗi tiếng Việt" }` — frontend hiện `message`. |
| Chưa làm | Trả **404 hoặc 501** — frontend hiện "chức năng đang được kết nối máy chủ" thay vì báo lỗi. |
| Xác thực | Cookie phiên (`credentials: include`). Khi có đăng nhập, trả **401** nếu hết phiên. |
| Ngày giờ | Hiện hiển thị nguyên chuỗi backend trả (`dd/mm/yyyy hh:mm`). Ô chọn ngày gửi lên dạng `yyyy-mm-dd`. |
| Tiền | VND là số nguyên; tiền tệ invoice theo trường `currency`. |

---

## 1. Đơn hàng — `features/orders`, `features/order-import`

> **Mã VA bill:** đơn cấp bill trên portal có mã `VA + chi nhánh + 7 số (OrderNumber, MAX+1) + mã nước ISO 2 chữ`, vd `VAHCM6003585US` (TP.HCM = VAHCM, Hà Nội = VAHN, Huế = VAHUE, Bảo Lộc = VABL, Cần Thơ = VACT), lưu ở cột mới `dbo.MaVanDon.VA_Bill`. `Order.branch` (vd "TP.HCM") và `receiver.countryCode` đọc lại từ mã này; `Order.remoteArea` = cột `Remote_Area_FedEx` ghi lúc cấp bill (vd "Fedex (Tier B)", null khi không thuộc VSVX). Mọi trường `bill` / `billCode` trả về mã này; đơn cũ (VA_Bill NULL) trả 7 số. Mọi API nhận mã bill (chi tiết, in, tra cứu) nhận cả `VAHCM6003585US` lẫn `6003585`.


| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/orders` | Có sẵn | Danh sách có lọc, sắp xếp, phân trang |
| GET | `/orders/senders?q=SGB` | **Mới** | Người gửi đã dùng ở đơn trước (cùng phạm vi danh sách đơn — tài khoản con chỉ thấy đơn mình tạo), tên công ty **hoặc** người liên hệ chứa `q` (1–100 ký tự, tối đa 20), mới nhất trước, mỗi người gửi (tên + SĐT + địa chỉ lấy hàng) 1 dòng → `[{ company, contact, phone, address, taxId, email, originalShipper, lastUsed }]`. Form tạo đơn: chỉ tài khoản con sửa / gợi ý tên công ty + địa chỉ lấy hàng (tài khoản chính khóa theo hồ sơ). Quyền `shipments.create` |
| GET | `/orders/receivers?q=SEE` | **Mới** | Người nhận khách đã gửi trước đây (từ đơn trong `dbo.MaVanDon`, cùng phạm vi với danh sách đơn — tài khoản con chỉ thấy đơn mình tạo), tên công ty chứa `q` (tối đa 20) — không có `q` là sổ địa chỉ (100 người nhận gần nhất); mới nhất trước, mỗi người nhận (tên + điện thoại + địa chỉ 1) 1 dòng → `[{ company, contact, phone, phoneCode, email, taxId, country, city, state, postalCode, address1, address2, address3, iossNo, eoriNo, lastUsed }]`. Quyền `shipments.create` |
| GET | `/orders/:bill` | Có sẵn | Chi tiết 1 đơn (khung "Chi tiết đơn hàng" + "Nhân bản đơn"): `Order` + `shipper { company, contact, tel, address, taxId, email }` + `receiver { company, contact, tel, country, city, postal, state, addr1, addr2, addr3, taxId, email }` + `packages [{ qty, packType, length, width, height, weightKg }]` (cân **1 kiện**, từ MaVanDon_PCS_DIM) + `invoice { currency, exportType, shippingFee, goodsValue, items [{ descEn, descVi, qty, unit, price, amount, hs, origin }] }` (từ MaVanDon_ChiTietHang) |
| POST | `/orders/import/preview` | Có sẵn | Kiểm tra file Excel tạo đơn (multipart), chưa tạo đơn — trang "Tạo đơn từ Excel" |
| POST | `/orders/import` | Có sẵn | Kiểm tra lại file và tạo đơn cho các dòng hợp lệ (≤ 100), cấp số vận đơn ngay |
| DELETE | `/orders/:bill` | Có sẵn | Hủy đơn (chỉ đơn "Chưa đi") |
| GET | `/orders/:bill/photos` | **Chưa làm** | Ảnh kiện chụp tại kho |
| GET | `/orders/:bill/events` | **Mới** | Hành trình đơn |
| GET | `/orders/print?bills=A,B&doc=` | Có sẵn | Trang **HTML** in chứng từ cho 1 hoặc nhiều đơn (≤ 100), tự mở hộp thoại in. `doc`: `bill-a4` \| `invoice` \| `cvck` \| `label-a6`, hoặc nhiều loại ngăn bằng dấu phẩy (VD `bill-a4,invoice,label-a6`) → trang gộp, mỗi loại 1 khung, in lần lượt từng loại (mỗi loại 1 hộp thoại in theo khổ giấy riêng). Mẫu theo hệ thống cũ: `invoice` (A4 dọc, 3 bản / đơn: SHIPPER, CONSIGNEE, Air waybill No. / Date / No. of pkgs / Weight / Dimensions, bảng hàng tên Anh/ Việt + nhà sản xuất - xuất xứ, HS, số lượng, đơn giá, thành tiền, Reason for Export và lời cam kết), `cvck` (công văn cam kết nội dung hàng xuất), `label-a6` (khổ 100×150 mm: chỉ shipping mark, 1 nhãn / kiện để dán thùng) |
| GET | `/orders/export` | Có sẵn | Bảng kê gửi hàng **.xlsx** (cùng tham số lọc như `GET /orders`, tối đa 10.000 dòng); tên file ở header `Content-Disposition` |

### GET `/orders` — query

`q, searchField (all|cnee|bill|ref|ct), status (all | danh sách wait,fly,nd,ok — nhóm lọc có thể trùng nhau: wait = chưa có ngày gửi, fly = có ngày gửi, nd = chưa có POD, ok = có POD), type (DOC|PACK), fromDate, toDate (yyyy-mm-dd), weightFrom, weightTo, page, pageSize (20|50|100), sortBy (seq|ref|bill|cnee|ct|sent|pod|created), sortDir (asc|desc)`

- `type`: dbo.MaVanDon không có cột loại hàng — DOC là đơn có tên hàng chứa "document", "chứng từ", "hồ sơ" hoặc từ "doc"/"docs"; còn lại là PACK (cùng quy tắc với cột `type` trả về).
- `fromDate`, `toDate`: theo ngày tạo đơn (`CreateDate`), tính cả 2 ngày đầu cuối.
- Tìm `bill` khớp một phần số VA hoặc mã hãng.
- **Thẻ tìm (nhiều giá trị):** `q` gồm nhiều thẻ, **mỗi thẻ 1 dòng** (ngăn bằng ký tự xuống dòng, URL-encode `%0A`), dạng `field:giá trị` với `field` ∈ `all|cnee|bill|ref|ct`. Dòng không có tiền tố hợp lệ dùng `searchField`. Tối đa 200 thẻ.
  - Thẻ **cùng trường** → khớp **bất kỳ** (OR); thẻ **khác trường** → phải khớp **tất cả** (AND).
  - VD người nhận "Uyen" ở Singapore hoặc Mỹ: `q=cnee:Uyen%0Act:Singapore%0Act:United States`.
  - VD dán cả cột bill: `q=bill:6010839%0Abill:6010532`.
- **Nhiều trạng thái:** `status` là `all` hoặc danh sách ngăn dấu phẩy, VD `status=wait,fly` → đơn có trạng thái thuộc danh sách. Áp dụng cho cả `GET /orders/export`.
- Không lọc chi nhánh: dbo.MaVanDon không có cột chi nhánh.
- `summary.statusCounts` đếm trên kết quả đã lọc (trừ lọc trạng thái) — số trên các tab luôn khớp bộ lọc.

Phản hồi (`OrderListResponse`):

```json
{
  "items": [{
    "id": "ord-1", "seq": 8, "bill": "6156979", "ref": "PO-A100", "connect": "1Z9A8X0312",
    "cnee": "LINEX CO. LTD", "ct": "Singapore", "route": "Chuyên tuyến - Singapore", "branch": "TP.HCM",
    "created": "09/09/2026 09:12", "sent": "09/09/2026", "type": "PACK",
    "st": "wait", "pcs": "1 kiện · 8.0 kg", "content": "CONSOL",
    "pod": { "date": "10/09/2026", "time": "14:20", "signer": "LIM" },
    "photos": 2
  }],
  "total": 8, "page": 1, "pageSize": 20, "totalPages": 1,
  "summary": {
    "statusCounts": { "all": 8, "wait": 2, "fly": 6, "nd": 4, "ok": 4 },
    "totalPieces": 16, "totalWeight": 237
  }
}
```

`summary` tính trên **toàn bộ kết quả lọc (trừ lọc trạng thái)** để số trên tab trạng thái đúng. Nên trả thêm `podEstimate` (dd/mm/yyyy); khi chưa có, frontend tự ước tính theo `TRANSIT_DAYS`.

### GET `/orders/:bill/photos`

```json
{ "data": [{ "url": "https://.../6156979-1.jpg", "caption": "Kiện 1/2 · 8kg", "takenAt": "08/09/2026 10:05" }] }
```

### GET `/orders/:bill/events` (mới nhất trước)

```json
{ "data": [{ "time": "09/09/2026 06:40", "title": "Đến kho nước đến", "location": "Singapore" }] }
```

### POST `/orders/import/preview` và `/orders/import` — multipart/form-data

| Trường | Bắt buộc | Mô tả |
|---|---|---|
| `file` | có | File **.xlsx** theo mẫu `web/public/templates/Mau_Excel_Tao_Don.xlsx` (sheet `DATA`, dòng 1 tiêu đề, dòng 2 chú thích, dữ liệu từ dòng 3; ≤ 5 MB, ≤ 100 dòng) |
| `service` | không | Dịch vụ áp cho cả file (vd `DHL`). Không gửi → lấy cột `Service` / `HUB` trong file nếu có |
| `hub` | khi có `service` | Vd `DHL - Singapore` |
| `branch` | không | Chi nhánh gửi |

Cột file (không phân biệt hoa thường): `Ref_No, Shipper_att, Shipper_Tel, Shipper_Tax, Shipper_Email, Cnee_country_Code, Cnee_company, Cnee_contact_name, Cnee_Tel, Cnee_Email, Cnee_TaxID, Cnee_Postalcode, Cnee_City, Cnee_State, Add1, Add2, Add3, Type (D|P), Description, Currency, Export_Type, Invoice_Value, Shipping_fee`; nhóm kiện `Qty_Pack_n, Pack_Type_n, L_n, W_n, H_n, GW_n` (GW là tổng cân của dòng kiện); nhóm hàng `Product_en_n, Product_vn_n, Manufacturer_n, Org_Country_n, HS_Code_n, Qty_n, Unit_n, Unit_Price_n` (n ≤ 50).

Kiểm tra: bắt buộc theo sheet HƯỚNG DẪN; mã nước 2 ký tự; bang bắt buộc với US/CA/AU; Add1, Add2 ≤ 30 ký tự; Description ≤ 50; HS code 6/8/10 chữ số; hàng hoá cần ≥ 1 sản phẩm và đủ D×R×C; chứng từ > 2 kg tự chuyển hàng hoá (cảnh báo). Ref_No trùng trong file hoặc đã có đơn → cảnh báo.

Phản hồi:

```json
{ "success": true, "message": "Đã tạo 2 đơn", "data": {
  "total": 3, "valid": 2, "invalid": 1, "created": 2,
  "rows": [{ "line": 3, "ref": "ABC12345", "type": "PACK", "consignee": "ABC LOGISTICS US", "countryCode": "US", "country": "United States",
    "city": "SAN ANGELO", "pieces": 1, "weightKg": 5, "chargeableKg": 7.2, "value": 100, "currency": "USD", "products": 1,
    "errors": [], "warnings": [], "bill": "90000012" }]
} }
```

`bill` chỉ có ở `/orders/import`. Mỗi đơn tạo ra được ghi kèm chi tiết kiện (`dbo.MaVanDon_PCS_DIM`) và dòng hàng (`dbo.MaVanDon_ChiTietHang`). Lỗi cả file (sai định dạng, thiếu cột, quá 100 dòng, chọn dịch vụ mà thiếu hub) → 400 kèm `message`.

---

## 2. Đơn nháp — `features/drafts`, `features/create-order`

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/drafts` | Có sẵn | Danh sách đơn nháp & chưa in = dòng `dbo.MaVanDon` có `Status = 0` (tối đa 500, mới trước). `id` = `MaVanDon.ID`; `stt` backend tự tính: `ready` khi đủ người gửi, người nhận (tên, nước, địa chỉ 1), dịch vụ, cân > 0, còn lại `draft`; `payload` dựng lại từ MaVanDon + 2 bảng chi tiết (thông tin không có cột như dịch vụ cộng thêm, DDU/DDP không giữ); `date` dạng `dd/MM/yyyy` |
| POST | `/drafts` | Có sẵn | Lưu đơn mới (nút "Tạo đơn hàng" / "Lưu nháp"): ghi `dbo.MaVanDon` `Status = 0`, `OrderNumber` NULL, `VA_Bill` = tiền tố chi nhánh (vd `VAHN`), kiện / dòng hàng vào 2 bảng chi tiết |
| PUT | `/drafts/:id` | **Mới** | Cập nhật đơn nháp đang sửa (mở từ nút "Sửa") — ghi đè dòng MaVanDon và kiện / dòng hàng; giữ nguyên người tạo. Đơn vừa được in / xóa ở nơi khác → 409 `DRAFT_CHANGED` |
| POST | `/drafts/:id/print` | Có sẵn | In & cấp mã bill cho chính dòng đó: cấp `OrderNumber`, `VA_Bill` đầy đủ, `Status = 1`, ghi VSVX → đơn khóa, chuyển sang Đơn hàng. Chưa đủ thông tin → 422 `DRAFT_NOT_READY`. Khóa dòng (UPDLOCK) trước khi cấp số: 2 người in cùng lúc → người sau nhận 409 `DRAFT_CHANGED`, không cấp số thứ 2 |
| DELETE | `/drafts/:id` | Có sẵn | Xóa hẳn đơn chưa in khỏi `dbo.MaVanDon` cùng kiện, dòng hàng. Đơn vừa được in ở nơi khác → 409 `DRAFT_CHANGED` (không xóa vận đơn đã cấp bill) |

Body POST / PUT (`NewDraft`):

```json
{
  "stt": "ready",
  "cnee": "LINEX CO. LTD", "ct": "Singapore", "service": "Chuyên tuyến - Singapore",
  "branch": "TP.HCM", "ref": "PO-A100", "pcs": "1 kiện · 8 kg", "content": "Váy nữ",
  "payload": { "...": "toàn bộ form CreateOrderValues — xem web/src/features/create-order/schema.ts" }
}
```

`stt`: `draft` = đang làm dở · `ready` = đã khai đủ, chờ in. **Backend phải lưu nguyên `payload`** (cột JSON / NVARCHAR(MAX)) để mở lại đơn sửa tiếp.

`payload.receiver` có thêm `countryCode` (ISO-2), `iossNo`, `eoriNo` (chuỗi, không bắt buộc).
Hai mã chỉ hiển thị / kiểm tra khi nước đến thuộc EU; nguồn danh sách 27 nước dùng chung FE/BE:
`web/src/shared/config/eu-countries.json` (không gồm GB, NO, CH).
IOSS: `^IM\d{10}$`; EORI: `^[A-Z]{2}[A-Z0-9]{1,15}$`. Giá trị trống hợp lệ; sai định dạng ở EU trả 400.
Đổi sang nước ngoài EU: frontend xóa giá trị và lỗi; backend cũng xóa hai mã khỏi payload nháp khi lưu.
Khi cấp bill, hai mã được lưu vào `dbo.MaVanDon.ConsigneeIossNo` / `ConsigneeEoriNo`
(migration `AddReceiverEuTaxNumbers`, hai cột nullable). `GET /orders/:bill` trả hai mã trong `receiver`, cùng `countryCode` cho nước EU.
`POST /drafts/:id/print` trả `{ "message": "...", "billCode": "6156980" }`. Ngay sau đó frontend mở `GET /orders/print?bills=<billCode>&doc=bill-a4` để in vận đơn khổ A4 — vì vậy `billCode` là **bắt buộc**, và bill vừa cấp phải in được ngay.

---

## 3. Danh mục, sổ địa chỉ — dùng trong Tạo đơn

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/catalog/categories` | Có sẵn | Nhóm hàng (bảng `dbo.NhomHangHoa`): nhóm yêu thích (của khách hoặc nhóm chung khách đã đánh dấu) trước, rồi nhóm của khách, rồi nhóm chung Việt An — `Category[]` |
| POST | `/catalog/categories` | Có sẵn | Khách thêm nhóm: body `{ name, isFavorite }` → `{ data: Category, message }`. Tên trống **400**, trùng (không phân biệt hoa thường, kể cả nhóm chung) **409** |
| PUT | `/catalog/categories/:id` | Có sẵn | Đổi tên / yêu thích nhóm của khách, body như POST. Nhóm chung hoặc của khách khác **404** |
| DELETE | `/catalog/categories/:id` | Có sẵn | Xóa nhóm của khách (đơn đã khai nhóm này giữ nguyên) |
| PUT | `/catalog/categories/:id/favorite` | **Mới** | Đánh dấu / bỏ yêu thích, body `{ isFavorite }` → `{ message }`. Được cả nhóm chung (lưu riêng cho khách trong `dbo.MatHangKhachHang`, Loai = `NHOM`); nhóm của khách cập nhật `NhomHangHoa.Yeu_Thich`. Không thấy nhóm **404** |
`Category`: `{ id, name, isFavorite, isOwn, suggestions: [{ en, vi, hs }] }` — `isOwn = false` là nhóm chung Việt An (`CustomerID` NULL), chỉ đọc.

| GET | `/catalog/addon-fees` | **Chưa làm** | Biểu phí tùy chọn dịch vụ: `AddonFee[]` = `{ name, fee: number \| null, currency, unit, note? }`. `name` khớp tên tùy chọn trên form (vd "Đóng gói hộ"); `currency` = `"%"` khi tính theo % (`unit` vd "giá trị hàng"); `fee` null = chưa có giá. Chưa có endpoint (404/501) → form hiện "Đang cập nhật" |
| GET | `/catalog/products` | Có sẵn | Thư viện mặt hàng của khách: `SavedProduct[]` — lấy từ các dòng hàng đã khai trong `dbo.MaVanDon_ChiTietHang` (đơn của khách), mỗi mặt hàng 1 dòng, đơn giá lần gần nhất. Mặt hàng yêu thích đứng đầu, mặt hàng khách đã xóa không trả về. `SavedProduct.id` = khóa mặt hàng (SHA-256 hex 64 ký tự của tên EN/VN, HS, xuất xứ, đơn vị, nhà sản xuất — không phân biệt hoa thường); `isFavorite` |
| PUT | `/catalog/products/:id/favorite` | **Mới** | Đánh dấu / bỏ yêu thích mặt hàng, body `{ isFavorite }` → `{ message }` (bảng `dbo.MatHangKhachHang`, Loai = `SP`). Khóa sai định dạng **400** |
| DELETE | `/catalog/products/:id` | **Mới** | Xóa mặt hàng khỏi thư viện — chỉ ẩn (`Da_Xoa = 1`), dòng hàng trong đơn cũ giữ nguyên. |
| POST | `/catalog/products` | Không làm | Mặt hàng tự vào thư viện khi đơn được cấp bill (ghi `MaVanDon_ChiTietHang`) — bảng không có cột khách nên không lưu riêng được |
| POST | `/invoices/import` | **Mới** | Đọc file Excel mẫu invoice (`/templates/Mau_Import_Invoice.xlsx`, multipart `file` ≤ 5 MB) → `{ data: { items: InvoiceItem[], skipped: string[] } }`; không lưu. Cột: STT, Product_en (bắt buộc, ≤ 170), Product_vn (≤ 170), Description = nhà sản xuất (≤ 300), Country (≤ 50, trống = VN), HS_Code, Quantyti (> 0, số nguyên với PCS/SET/BOX/BAG), Unit (trống = PCS), Unit_Price (≤ 2 số lẻ); dòng 2 chú thích bỏ qua; tối đa 49 dòng. Dòng lỗi vào `skipped` dạng "Dòng 5: lý do". Sai mẫu / không đọc được → 400 |
| GET | `/catalog/hs-lookup?country=US&q=&mode=catalog` | Có sẵn | Tra mã HS theo nước nhập khẩu từ `dbo.Catalog_HsCode` (bảng người dùng tạo và nạp dữ liệu, portal chỉ đọc). `country`: `US` (HTSUS) \| `GB` (UK Tariff, khớp cả dữ liệu ghi `UK`) \| `AU` (ABF); `q` ≥ 2 ký tự: toàn số (cho phép dấu chấm / khoảng trắng) → so đầu `NationalCode`, còn lại → mọi từ phải có trong `NameVi` / `NameEn` / `NationalDescription` (không phân biệt hoa thường và dấu: `xoai say` khớp `Xoài sấy`); `mode`: `catalog` (toàn biểu thuế) \| `verified` (mặc định, chỉ `IsVerified = 1`). → `{ data: [{ nameVi, nameEn, hs6, nationalCode, nationalDescription, unit, note, isVerified, path: string[] }] }`, tối đa 50 dòng, đã xác minh trước rồi theo mã. `path` = các cấp cha tách từ mô tả US / AU dạng `A > B > tên dòng`; chữ đã bỏ thẻ HTML / `&#160;`. Dữ liệu AU lấy về có lỗi: mã lẫn `?` (`?0805.40.00.26`) và đuôi `.160` được làm sạch khi trả; dòng không có mã HS (chú thích lấy nhầm) bị ẩn; đơn vị `..` = không có. Không trả thuế suất (`DutyRate`). Sai nước / từ khóa quá ngắn → `data: []` |
| GET | `/invoices/recent?limit=20` | Có sẵn | Invoice đơn gần đây (≤ 50, từ `MaVanDon_ChiTietHang`) để chép lại: `{ bill, cnee, date, currency, items: SavedProduct[] }[]` |
| GET | `/geo/countries` | Có sẵn | Quốc gia + mã điện thoại: `[{ code: "US", name: "United States", dialCode: "+1" }]` (backend lấy từ world-countries, cache 1 ngày) |
| GET | `/geo/postal/:countryCode/:postalCode` | Có sẵn | Mã bưu chính → `{ countryCode, postalCode, city, state, stateCode, places: [{ city, state, stateCode, area }] }` — `city/state/stateCode` = nơi đầu tiên; `places` = mọi nơi khác nhau dùng chung mã (tối đa 20): nhiều thành phố thì form không tự điền, khách chọn; `area` = khu phố / bưu cục nhỏ hơn thành phố (Mexico: colonia, thành phố = municipio adminName2; Ấn Độ: locality, thành phố adminName3), null ở nước khác (backend tra GeoNames postalCodeLookupJSON maxRows=30, ~100 nước, nhận cả mã đầy đủ như `SW1A 1AA`; tài khoản GeoNames chỉ ở cấu hình backend `GeoNames:Username`); không tìm thấy / chưa hỗ trợ / nguồn tạm lỗi → 404 |
| GET | `/geo/postal-search?country=MX&q=1609` | **Mới** | Gợi ý mã bưu chính khi khách gõ (`q` ≥ 2 ký tự): mã bắt đầu bằng `q` (GeoNames postalCodeSearchJSON `postalcode_startsWith`, tối đa 15 dòng, cache 1 ngày); không có mã nào bắt đầu như vậy → tra đúng mã như `/geo/postal` → `[{ postalCode, city, state, stateCode, area }]` (cùng quy tắc `places`). Nguồn lỗi / nước sai → `[]`. Form hiện `"16090, Barrio San Pedro-Xochimilco"` (area-city) và `"16090, Xochimilco"`; chọn → điền mã + thành phố, không điền tỉnh / bang |
| GET | `/geo/remote-areas?country=US&postal=10001&city=New York` | **Mới** | Vùng sâu vùng xa (VSVX / ODA): tra bảng `dbo.VungXauVungXa` của hệ thống cũ theo mã nước ISO + mã bưu chính (khoảng Begin–End, so phần đầu mã; Anh theo vùng / quận / khu) + thành phố (dòng có thành phố phải trùng, không phân biệt hoa thường / dấu) → `[{ carrier, tier }]` (`tier` = cột To Parcel Services, vd "Tier A" của FedEx; null khi trống / "No"). Mỗi nước cache 1 ngày. Không thuộc vùng nào / chưa có bảng → `[]`. Form hiện "Khu vực thuộc VSVX của: Fedex (Tier A), UPS" |
| GET | `/geo/addresses?country=US&q=123 Main` | **Mới** | Gợi ý địa chỉ khi khách gõ ô Địa chỉ 1 (backend gọi Geoapify Address Autocomplete, lọc theo nước, tối đa 6 dòng, cache 1 ngày; key chỉ ở cấu hình backend `Geoapify:ApiKey`) → `[{ label, address1, city, state, stateCode, postalCode, countryCode }]`. `q` dưới 3 ký tự, nước sai, chưa cấu hình key hoặc nguồn tạm lỗi → `[]`. `address1` rỗng khi gợi ý chỉ tới cấp thành phố / mã bưu chính |
| GET | `/addresses/senders` | **Chưa làm** | Hồ sơ người gửi: `{ id, n (tên), c (liên hệ), t (điện thoại), d (địa chỉ) }` |
| — | `/addresses/receivers` | **Bỏ** | Sổ địa chỉ người nhận lấy từ đơn cũ: `GET /orders/receivers` (không có `q`) — không lưu sổ riêng |

`SavedProduct`: `{ id?, descEn, descVi, manufacturer, origin, hs, unit, price }` (tất cả chuỗi).

Nên làm thêm (hiện khai tĩnh ở `shared/config/domain.ts`): `GET /services/hubs` (hãng → danh sách hub), `GET /addresses/countries`, `GET /branches`, `GET /services/limits` (giới hạn kích thước theo hãng — hiện ở `create-order/lib/carrier-limits.ts`).

---

## 4. Giá & gợi ý dịch vụ — `features/pricing`

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| POST | `/rates` | **Chưa làm** | So sánh giá. Cũng được gọi khi bấm "Tạo đơn" để kiểm tra phụ phí |
| GET | `/services` | **Chưa làm** | Danh sách dịch vụ & bảng giá |
| POST | `/services` | Không dùng | Portal khách không nhập / sửa bảng giá — Việt An quản lý nội bộ |
| DELETE | `/services/:id` | Không dùng | Như trên |

`POST /rates` body: `{ country, weight, length?, width?, height?, type: "DOC"|"PACK" }` → `{ "rates": ServiceQuote[] }`
`ServiceQuote`: `{ name, zone, chargeableWeight, volumetricWeight, baseFare, fscFee, surcharges, hasSurcharge, vatFee, totalFare, eta }`.

`ShippingService`: `{ id, name, account, fsc (0.28 = 28%), vat, eta, effFrom, effTo (yyyy-mm-dd), zones: string[], zmap: { [country]: zoneNo }, dz, price: { [zoneNo]: number[140] }, over70: { [zoneNo]: number }, sur: SurchargeRule[] }`.
`price[zone]` là 140 mốc 0.5kg → 70kg. `SurchargeRule`: `{ wFrom, wTo, dFrom, dTo, gFrom, gTo, fee }` — ô rỗng `""` = không giới hạn.
Gợi ý bảng SQL: `Services`, `ServiceZones`, `ServiceCountryZones`, `ServicePrices (serviceId, zoneNo, stepIndex, price)`, `ServiceSurcharges`.

---

## 5. E-commerce — `features/ecommerce`

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/ecom/orders?scope=&src=&q=` | Có sẵn | Đơn E-commerce (`dbo.DonTMDT`, không liên quan `MaVanDon`). `scope`: `inbox` = tab Đơn hàng của trang E-commerce (chưa xác nhận), `mine` = trang **Đơn hàng E-com** `/ecommerce/orders` trên menu Dịch vụ & Bán hàng (đã xác nhận gửi), trống = tất cả. Lọc theo nguồn & từ khóa; mới nhất trước, tối đa 500 |
| POST | `/ecom/manual` | Có sẵn | Lưu 1 đơn nhập tay vào `dbo.DonTMDT`, **vào thẳng trang Đơn hàng E-com** (đã xác nhận). Body `NewManualEcomOrder` dưới; `source` ∈ manual, tiktok, shopee, lazada, amazon, ebay, etsy, woocommerce → `{ data: EcomOrder, message }`. Trùng mã đơn cùng nguồn → 409 |
| POST | `/ecom/import-csv` | Chưa làm | `{ "csv": "<nội dung file>" }` → `{ message, importedCount, errors: [{ row, message }] }` theo mẫu 70 cột `public/templates/VietAn_Ecom_Import_Template.csv`. Frontend đang để trong `PENDING_ENDPOINTS` (hiện "đang hoàn thiện") |
| POST | `/ecom/orders/confirm` | Có sẵn | `{ ids }` → `{ message, count }`. Xác nhận gửi → sang trang Đơn hàng E-com (cột `Ngay_Xac_Nhan`). Đơn thiếu trường bắt buộc (tên, địa chỉ + nước, SĐT, sản phẩm) bị bỏ qua và nêu trong `message`. Đồng bộ lại không ghi đè đơn đã xác nhận |
| POST | `/ecom/orders/unconfirm` | Có sẵn | `{ ids }` → `{ message, count }`. Trả đơn đã xác nhận (chưa có bill) về tab Đơn hàng của trang E-commerce |
| POST | `/ecom/orders/delete` | Có sẵn | `{ ids: string[] }` (≤ 500) → `{ message, deletedCount }`. Xóa mềm đơn chưa gửi (cột `Ngay_Xoa`); đơn đã xác nhận gửi hoặc đã có bill không xóa được và được giữ lại. Đơn sàn đã xóa không bị đồng bộ tạo lại; nhập lại file export có đơn đó thì khôi phục |
| PUT | `/ecom/orders/:id` | Có sẵn | Sửa đơn chưa có bill: `{ receiver: { name, company, phone, email, address1, address2, city, state, postal, countryCode }, kg, products: [{ name, sku, qty, fobPrice, sellingPrice, hsCode }], service, hub, branch, note }` → `{ data: EcomOrder, message }`. Đã sửa thì đồng bộ / nhập lại từ sàn không ghi đè. Đã có bill → 422 |
| POST | `/ecom/webhook/:platform` | Có sẵn | Sàn đẩy đơn vào (không do frontend gọi) |
| ~~POST~~ | ~~`/ecom/labels`~~ | Không dùng | In nhãn dán kiện A6, phiếu đóng gói A4, bảng kê giao hàng A4 tạo ngay trên trình duyệt từ dữ liệu `GET /ecom/orders` (`features/ecommerce/lib/print-docs.ts`, mã vạch Code 128 theo mã đơn shop) — backend không cần endpoint in |
| GET | `/ecom/settings` | **Chưa làm** | `EcomSettings` |
| PUT | `/ecom/settings` | **Chưa làm** | Cập nhật một phần `EcomSettings` → trả bản đầy đủ |
| POST | `/ecom/settings/api-keys/regenerate` | **Chưa làm** | `{ env: "production"\|"sandbox" }` → `EcomSettings` |
| POST | `/ecom/settings/webhook/test` | **Chưa làm** | Gửi sự kiện thử tới URL webhook |

`NewManualEcomOrder`: `{ ref, source, cnee, phone, email?, ct, countryCode?, postal, city, state, address, kg, products: [{ name, sku, qty, fobPrice, sellingPrice }] }` — chỉ các trường đơn sàn nào cũng có (bắt buộc: ref, cnee, phone, ct, address, ≥ 1 sản phẩm); `kg` = 0 nếu Việt An cân. Dịch vụ / hub / chi nhánh / mã HS / khai hải quan do Việt An bổ sung khi nhận hàng.
`ct` là tên nước tiếng Anh lấy từ `GET /geo/countries`, `countryCode` là mã ISO 2 ký tự (trống khi danh sách nước tạm lỗi). Form tự điền `city`, `state` từ `GET /geo/postal` (GeoNames), hoặc `addr1`, `city`, `state`, `postal` khi khách chọn 1 gợi ý của `GET /geo/addresses` (Geoapify); khách vẫn sửa được.
`EcomOrder` (GET /ecom/orders, POST /ecom/manual): `{ id, src, ref, bill, cnee, ct, items, kg, st, note?, products?: [{ name, sku, qty, fobPrice, sellingPrice, hsCode? }], createdAt: "dd/MM/yyyy HH:mm", value?, currency?, receiver?: { name, company, phone, email, address1, address2, city, state, postal, countryCode, country }, service?, hub?, branch?, issues: string[] (trường bắt buộc còn thiếu trước khi xác nhận gửi: tên, địa chỉ / nước, SĐT, sản phẩm — cân nặng, mã HS, chữ Latin không bắt buộc), editable, confirmed, confirmedAt, deletable }` — `deletable` = đơn nhập tay / file chưa gửi; đơn đồng bộ từ shop đã kết nối không xóa trong portal (hủy / hoàn tất trên sàn), đồng bộ luôn hiện lại đơn đó kể cả đơn từng bị xóa.
`EcomSettings`: `{ apiKeys: [{ env, key }], webhookUrl, webhookEvents: ("created"|"picked_up"|"departed"|"delivered"|"exception")[] }`. Trên giao diện, phần API key / webhook nằm trong mục thu gọn "Dành cho lập trình viên" ở tab Kết nối.

### 5.1 Kết nối sàn qua OAuth (TikTok Shop) — tab `?tab=connect`

Tích hợp Shopify đã gỡ (tháng 10/2026). Hiện chưa có adapter kênh nào: `stores/connect` và `stores/:id/sync` luôn trả 422 `ECOM_CHANNEL_UNAVAILABLE`.

Nghiên cứu sàn và thiết kế backend: `docs/ECOM_INTEGRATION.md`.

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/ecom/stores` | Có sẵn | `{ data: StoreConnection[] }` của khách đang đăng nhập |
| POST | `/ecom/stores/connect` | Có sẵn (mọi kênh trả 422 "chưa hỗ trợ") | `{ platform: "tiktok", shopDomain?, region?: "global"\|"us" }` → `{ authorizeUrl }` khi có adapter. Backend tạo `state` ngẫu nhiên gắn với khách (hết hạn 10 phút); frontend chuyển trình duyệt sang `authorizeUrl` |
| GET | `/ecom/oauth/tiktok/callback` | **Mới** | TikTok Shop redirect về với `code`, `state`: đổi token, lấy `shop_cipher`, rồi **302** về `/ecommerce?tab=connect&connected=tiktok` hoặc `&error=<thông báo>` |
| POST | `/ecom/stores/:id/sync` | Có sẵn (422 "chưa hỗ trợ") | Kéo đơn mới từ sàn vào `dbo.DonTMDT` → `{ message, importedCount }`; token hết hạn tự làm mới, không làm mới được → 422 + trạng thái `expired` |
| DELETE | `/ecom/stores/:id` | Có sẵn | Xóa token, ẩn shop khỏi danh sách → `{ message }` |
| POST | `/ecom/webhook/tiktok` | **Mới** | TikTok Shop đẩy sự kiện (đổi trạng thái đơn, hủy, thu hồi ủy quyền). Kiểm chữ ký header `Authorization` |

`StoreConnection`: `{ id, platform: "tiktok" (hoặc "shopify" với kết nối cũ trước khi gỡ tích hợp), shopName, shopDomain?, region?, status: "active"|"expired"|"error"|"revoked", connectedAt, lastSyncAt?, lastError? }`.
Token sàn (access/refresh) **chỉ lưu ở backend** (mã hóa), không bao giờ trả về frontend.
`state` OAuth tự chứa và ký HMAC (mã khách, shop, domain portal, hạn 10 phút), kèm cookie nonce `vae_ecom_oauth` (SameSite=Lax, path `/api/v1/ecom/oauth`) — cookie đăng nhập là SameSite=Strict nên không đi kèm callback từ sàn. URL callback ghép từ domain portal khách đang dùng (X-Forwarded-Host của proxy Vercel, chỉ nhận domain trong `Company:PortalUrl` / `Cors:AllowedOrigins`); mỗi domain phải khai trong danh sách redirect URL của app trên sàn.
Đơn nhận từ sàn xuất hiện trong `GET /ecom/orders` với `src` = `tiktok`…; kết nối luôn **tự nhận đơn mới** (webhook + nút Đồng bộ). **Chưa làm:** đơn E-com xác nhận gửi chưa được cấp bill (`dbo.MaVanDon`) và chưa tự đẩy mã tracking lên sàn (`fulfillmentCreate`) — dự kiến khi nối luồng E-com → cấp bill.
Chủ shop thu hồi ủy quyền → kết nối chuyển `status: "revoked"` (frontend hiện "Shop đã gỡ ứng dụng", nút ủy quyền lại).

---

## 6. Hỗ trợ & sự cố, Thông báo

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/troubles` | **Mới** | `{ data: TroubleTicket[] }` — 200 yêu cầu cập nhật gần nhất; tài khoản con chỉ thấy yêu cầu mình gửi. `TroubleTicket` = `{ id: "TRB-12", kind: "su_co" \| "ho_tro" \| "gop_y", bill (trống nếu không phải sự cố), cnee, ct, type (loại sự cố / chủ đề), subject (tiêu đề yêu cầu hỗ trợ, null), lv, desc, req, contact, date, status, updatedAt }`. Lưu ở bảng cũ `dbo.TContact` (`FormType` "Portal - Sự cố" / "Portal - Hỗ trợ" / "Portal - Góp ý", `Status` 0 mới · 1 đang xử lý · 2 chờ khách · 3 đã xử lý) để phần mềm cũ hiện và xử lý. Không có trao đổi qua lại. Quyền `account.troubles` (tài khoản con luôn có) |
| POST | `/troubles` | **Mới** | `multipart/form-data`: `kind` (`su_co` mặc định / `ho_tro` / `gop_y`), `bill`, `cnee?`, `ct?`, `type`, `subject`, `lv` (`low`\|`mid`\|`high`), `desc` (10–4000), `req`, `contact`, `file?` (≤ 10 MB: ảnh, PDF, Excel, CSV, Word; chỉ gửi kèm email về hộp thư Việt An, không lưu database — `Content` ghi thêm tên tệp) → `{ data: TroubleTicket, message }`. **Sự cố:** bắt buộc `bill` (vận đơn của khách — VA Bill, số VA hoặc mã hãng; không có → 400 `TROUBLE_BILL_NOT_FOUND`; `cnee` / `ct` trống thì lấy từ vận đơn), `type`, `lv`, `req`, `contact`. **Hỗ trợ:** bắt buộc `type` (chủ đề), `subject` (5–200), `contact`. **Góp ý:** chỉ cần `desc`. `req` trống → tên đăng nhập; `lv` trống → `low`. Gửi email báo về hộp thư nhận (ở nền, kèm tệp). Sửa thông tin người gửi = yêu cầu hỗ trợ chủ đề "Sửa thông tin người gửi" (CS sửa ở phần mềm cũ) |
| GET | `/notifications` | **Mới** | 50 thông báo mới nhất → `{ data: [{ id, imp, unread, title, date: "dd/MM/yyyy HH:mm", body, link }], unreadCount }` (`link`: nút "Xem tại đây", null nếu không có) — `imp: true` hiện popup khi mở portal. Gồm thông báo chung (`CustomerID` trống) và thông báo riêng của khách (vd phản hồi sự cố). Quyền `account.notifications` (tài khoản con luôn có). Đã đọc lưu riêng từng tài khoản (chính / con) ở `dbo.ThongBao_DaDoc` |
| POST | `/notifications/:id/read` | **Mới** | Đánh dấu đã đọc → `{ message }`; không có → 404 `NOTIFICATION_NOT_FOUND` |
| POST | `/notifications/mark-all-read` | **Mới** | Đánh dấu đã đọc 50 thông báo mới nhất → `{ message }` |

`TroubleTicket`: `{ id, bill, cnee, ct, type, lv, desc, req, contact, date, status: "new"|"doing"|"waitc"|"done", reply? }`. Nên chuyển `reply` thành danh sách tin nhắn `messages: [{ from: "customer"|"cs", text, at }]` — frontend sẽ đổi sang hiển thị hội thoại.

---

## 7. Tài khoản & hỗ trợ

### 7.1 Tài khoản chính (admin) và tài khoản con của nhân viên

- **Tài khoản chính** = login ở `dbo.TCustomer` (`accountType: "customer"`, `isAdmin: true`): nhận mọi quyền của khách,
  gồm 2 quyền chỉ admin có: `account.staff` (quản lý nhân viên) và `account.mytracking` (cấu hình MyTracking).
- **Tài khoản con** (`accountType: "staff"`, `isAdmin: false`) là người liên hệ có tên đăng nhập trong bảng cũ `dbo.TCustomer_NguoiLienLac` (`TinhCach` = tên đăng nhập, `Mo_Ta` = mật khẩu lưu như hệ thống cũ; tài khoản tạo trước khi chuyển bảng còn mã băm PBKDF2 vẫn đăng nhập được; quyền ở cột `Quyen` portal thêm; `DELETE` chỉ gỡ đăng nhập, giữ dòng người liên hệ):
  đăng nhập cùng `POST /auth/login` (thử `dbo.TCustomer` trước, không khớp thì thử tài khoản con), JWT có thêm claim `staff_id`,
  `customer_id` là khách cha, nhân viên chỉ có các quyền admin đã chọn.
  `SessionUser` của tài khoản con: `companyName`, `address`, `taxCode` của công ty; `contactName` = họ tên nhân viên,
  `phone` / `email` = của nhân viên (null nếu chưa khai, không lấy của công ty) — form Tạo đơn điền sẵn người gửi theo đó.
- **Phạm vi đơn của nhân viên:** chỉ thấy vận đơn và đơn nháp **do mình tạo** (danh sách, chi tiết, hành trình, in, xuất Excel,
  thư viện mặt hàng). Người tạo vận đơn lưu ở cột có sẵn `dbo.MaVanDon.Customer_Staff_ID` (= `TCustomer_NguoiLienLac.ID`; 0 = tài khoản chính / hệ thống cũ),
  nháp cũng là dòng `dbo.MaVanDon` nên dùng chung cột này, sửa nháp không đổi người tạo; in từ nháp thì vận đơn thuộc người tạo nháp (admin in hộ vẫn là đơn của nhân viên).
  Quyền `shipments.view-all` ("Xem toàn bộ đơn của công ty", admin tick cho từng người) → thấy mọi đơn của công ty.
  Tài khoản chính luôn thấy tất cả.
- **Quyền E-commerce:** `ecommerce.view` (xem đơn & cửa hàng), `ecommerce.orders` (nhập tay, nhập CSV, sửa, xác nhận / trả về, xóa, đồng bộ),
  `ecommerce.connect` (kết nối / ngắt cửa hàng), `ecommerce.view-all` (xem toàn bộ đơn E-com của công ty).
  Tài khoản con không có `ecommerce.view-all` chỉ thấy / xử lý đơn mình nhập tay hoặc nhập CSV (`dbo.DonTMDT.Nhan_Vien_Tao`);
  đơn tự đồng bộ từ sàn không có người tạo nên chỉ tài khoản chính và người có `ecommerce.view-all` thấy.
  Tài khoản con cũ chỉ có `ecommerce.connect` cần được admin cấp thêm `ecommerce.orders` để tiếp tục xử lý đơn.
  Tên đăng nhập duy nhất toàn hệ thống (không trùng tài khoản con khác hay `Login_UserName` của khách nào).
  Khóa / đặt lại mật khẩu / xóa → refresh token cũ bị từ chối (nhân viên phải đăng nhập lại khi access token hết hạn).
  Đổi quyền áp dụng ở lần làm mới phiên tiếp theo. Tài khoản bị khóa đăng nhập trả 401 `ACCOUNT_DISABLED`.
- Frontend ẩn menu / chặn trang theo `SessionUser.permissions` (`features/auth/lib/permissions.ts`); backend vẫn kiểm tra `[HasPermission]`.

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/account/staff` | Có sẵn | Quyền `account.staff`. `{ data: StaffAccount[] }` của khách đang đăng nhập. `permissions` chỉ gồm quyền tài khoản chính đã chọn (không có quyền tài khoản con luôn có như `account.troubles`; gửi kèm khi tạo / sửa thì backend bỏ qua) |
| GET | `/account/staff/permissions` | Có sẵn | `{ data: [{ code, description }] }` — quyền admin cấp được cho nhân viên (mọi quyền của khách trừ `account.*`) |
| POST | `/account/staff` | Có sẵn | `{ userName, password, fullName, email?, phone?, phone2?, email2?, permissions: string[] }` → `{ data: StaffAccount, message }`. `userName` 3–50 ký tự `[A-Za-z0-9._@-]`; mật khẩu ≥ 8 ký tự có chữ và số. Trùng tên → 409 `USERNAME_TAKEN`; quyền không cấp được → 400 `UNKNOWN_PERMISSION`; tối đa 100 tài khoản / khách |
| PUT | `/account/staff/:id` | Có sẵn | `{ fullName, email?, phone?, phone2?, email2?, permissions, active }` → `{ data: StaffAccount, message }` — `active: false` = khóa; gửi đủ hồ sơ (ô bỏ trống = xóa giá trị) |
| POST | `/account/staff/:id/reset-password` | Có sẵn | `{ newPassword }` → `{ message }` |
| DELETE | `/account/staff/:id` | Có sẵn | `{ message }` — đơn nhân viên đã tạo vẫn thuộc khách cha |

`StaffAccount`: `{ id, userName, fullName, email?, phone?, phone2?, email2?, permissions: string[], active, createdAt }` (giờ Việt Nam, không offset). Cột bảng cũ: `fullName` = `Ten`, `phone` / `phone2` = `Di_Dong` / `Di_Dong2`, `email` / `email2` = `Email` / `Email2`; `Chuc_Vu`, `Phong_Ban` ghi 0, `Gioi_Tinh` để NULL (portal không dùng).
Tài khoản con gọi các endpoint trên → 403. Tài khoản con **không tự đổi mật khẩu**: `POST /auth/change-password` cần quyền `account.password` (chỉ tài khoản chính có, không cấp được cho nhân viên) → tài khoản con nhận 403; admin đặt lại qua `POST /account/staff/:id/reset-password`. Frontend ẩn mục Đổi mật khẩu theo quyền này.

### 7.2 MyTracking cá nhân

Trang `/account/mytracking` (chỉ admin) cấu hình trang tra cứu mang thương hiệu của khách: tiêu đề, mô tả,
tối đa 5 ảnh quảng cáo có link đích, tiêu đề ảnh (120 ký tự) và chữ nút (40 ký tự) tùy chọn,
cùng 1 hình nền. Cấu hình lưu trên server trong `dbo.TCustomer_MyTracking` (1 dòng / khách) và
`dbo.TCustomer_MyTracking_Banner` (1 dòng / ảnh, liên kết bằng `MyTrackingID`), dữ liệu API JSON ≤ 2 MB, cùng **đường dẫn**
`slug` và trạng thái **xuất bản**. Đã xuất bản thì ai có link `/t/{slug}` cũng xem được và tra cứu vận đơn
(`POST /public/tracking`); `?bills=MA1,MA2` mở sẵn kết quả. Nếu `GET /account/mytracking` trả 404/501 (backend cũ),
frontend quay về lưu localStorage theo `customerCode` (key `mytracking-draft:{customerCode}`) như bản thử nghiệm.

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| GET | `/account/mytracking` | Có sẵn | Quyền `account.mytracking`. `{ data: { slug, published, config, updatedAt? } }` — chưa cấu hình: `config: null`, `slug` gợi ý theo mã khách |
| PUT | `/account/mytracking` | Có sẵn | `{ slug, published, config }` → như GET + `message`. `slug` 3–60 ký tự `^[a-z0-9]+(-[a-z0-9]+)*$`; đã có khách khác dùng → 409 `SLUG_TAKEN`; lần lưu đầu ở 2 tab cùng lúc → 409 `MYTRACKING_CHANGED`; `config` phải là object, ≤ 2 MB |
| GET | `/public/mytracking/:slug` | Có sẵn | Không cần đăng nhập. `{ data: { slug, config } }` khi đã xuất bản; chưa xuất bản / không có → 404 |

`config` theo `myTrackingSchema` (`features/mytracking/schema.ts`); backend tách các trường vào cột hiện có và ảnh vào bảng banner,
giữ thuộc tính bổ sung trong `Cau_Hinh_Bo_Sung`, rồi tái dựng JSON cùng cấu trúc khi đọc. Frontend kiểm tra lại bằng `parseConfig` khi đọc
(link chỉ nhận http/https, ảnh http/https hoặc data URL PNG/JPEG/WebP).
Preview trong trang cấu hình hiển thị vùng chờ kết quả tra cứu, không gọi API tracking. Có logo, thông tin thương hiệu và các
link mạng xã hội, chuyển Desktop / Mobile và cập nhật trực tiếp từ form. Ảnh JPG/JPEG/PNG/WebP
tối đa 200 KB được nén bằng canvas xuống tối đa 200 KB trước khi lưu base64.
Ảnh từ URL được kiểm tra tải/giải mã trước khi thêm (timeout 15 giây), giữ link gốc;
không tải về lưu hoặc nén. Giới hạn 200 KB sau nén áp dụng cho ảnh tải từ máy.
`ConfigRepository` tách lưu trữ khỏi component: `ApiConfigRepository` (server) và `LocalStorageConfigRepository` (dự phòng).

Form chia khối theo mẫu HTML: thương hiệu, nội dung chính, quảng cáo, nền trang và liên hệ.
Ảnh có thể đổi thứ tự bằng kéo thả hoặc nút lên/xuống. Tiêu đề và chữ nút hiển thị bên dưới ảnh;
nút hiện khi có chữ trên nút, nhưng bị vô hiệu hóa nếu chưa có link website hợp lệ.
Ô “Link website của ảnh” lưu vào `images[].linkUrl`; ảnh và nút cùng mở link này trong tab mới.
Để trống chữ trên nút thì ẩn nút, ảnh vẫn mở link nếu có. Zalo/WhatsApp nhận URL http(s) hoặc số điện thoại,
chuyển thành link zalo.me/wa.me; số bắt đầu bằng 0 được hiểu là số Việt Nam (+84).
Liên hệ để trống thì không hiện nút trong preview. Cấu hình ảnh cũ không có tiêu đề/chữ nút vẫn đọc được.

### 7.3 Đăng nhập & hỗ trợ

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| POST | `/auth/change-password` | **Mới** | `{ currentPassword, newPassword }` — sai mật khẩu hiện tại trả 400 + `message`. Quy tắc: ≥ 8 ký tự, có chữ và số. Ghi vào `dbo.TCustomer.Login_Password` (dạng như hệ thống cũ); các thiết bị khác phải đăng nhập lại |
| POST | `/admin/login` | **Mới** | Trang quản trị /admin: `{ userName, password }` (tài khoản cố định trong code, chỉ lưu mã băm) → `{ data: { token, expiresAt, userName } }` — gửi lại bằng header `Authorization: Bearer`, không dùng cookie của khách |
| GET | `/admin/notifications` | **Mới** | 200 thông báo chung mới nhất (không gồm thông báo riêng từ sự cố) → `[{ id, title, body, link, important, createdAt, createdBy, emailStatus, emailRecipients, emailSent, emailError, readCount }]`; `emailStatus` ∈ `none \| sending \| sent \| partial \| failed \| no_recipients`. Quyền `admin.notifications` (chỉ va_admin) |
| POST | `/admin/notifications` | **Mới** | `{ title (≤ 200), body (≤ 4000), link? (http/https, ≤ 500 — sai → 400 `NOTIFICATION_LINK`), important, sendEmail }` → `{ data: AdminNotification, message }`. Lưu `dbo.ThongBao` (link ở cột `Duong_Dan`), hiện cho mọi khách; email kèm dòng "Xem tại đây: link". `sendEmail` → gửi email ở nền (BCC lô 50) tới email hồ sơ của khách có tài khoản portal + email tài khoản con đang hoạt động, tối đa 450 địa chỉ / lần (giới hạn Gmail); chưa cấu hình email → 400 `EMAIL_NOT_CONFIGURED` |
| GET | `/admin/troubles` | **Mới** | 300 yêu cầu (sự cố / hỗ trợ / góp ý) cập nhật gần nhất của mọi khách → `[{ ticket: TroubleTicket, customerCode, companyName, isStaff }]`. Quyền `admin.troubles` (chỉ va_admin) |
| POST | `/admin/troubles/:id/reply` | **Mới** | `{ reply?, status? }` (ít nhất 1; `status` ∈ `new \| doing \| waitc \| done`) → `{ data, message }`. Đổi `TContact.Status`; `reply` là ghi chú chỉ gửi trong thông báo **riêng khách đó** (`dbo.ThongBao.CustomerID`), nút "Xem tại đây" mở đúng yêu cầu (`/troubles?id=TRB-…`) |
| DELETE | `/admin/notifications/:id` | **Mới** | Xóa thông báo và lượt đã đọc → `{ message }` (email đã gửi không thu hồi) |
| POST | `/auth/login` | **Mới** | `{ username, password, remember }` — `username` là tên đăng nhập của khách (`dbo.TCustomer.Login_UserName`), mật khẩu so với `Login_Password`; không khớp thì thử tài khoản con của nhân viên (§7.1). Đúng: đặt cookie phiên (httpOnly; `remember: false` → cookie hết khi đóng trình duyệt) và trả `{ data: SessionUser }`. Sai: 401 + `message` |
| GET | `/auth/session` | **Mới** | Phiên hiện tại cho web portal, **luôn 200**: đã đăng nhập → `{ data: SessionUser }`; access token hết hạn mà còn refresh cookie → làm mới (đặt lại cookie) rồi trả người dùng; chưa đăng nhập → `{ data: null }`. Frontend dùng thay `/me` để mở trang khi chưa đăng nhập không sinh lỗi 401 |
| POST | `/auth/refresh` | Có sẵn | Đổi refresh token (cookie) lấy phiên mới — frontend tự gọi 1 lần khi gặp 401 |
| POST | `/auth/logout` | **Mới** | Thu hồi phiên của thiết bị này ở máy chủ (`dbo.PhienThuHoi`, theo claim `sid` của refresh / access token — mọi token của phiên bị từ chối ngay, kể cả response làm mới phiên về muộn) rồi xóa cookie. Thiết bị khác không bị ảnh hưởng. Mọi response `/api` mặc định `Cache-Control: no-store` |
| GET | `/me` | **Mới** | `{ data: SessionUser }` (`customerCode, companyName, contactName, email, phone, address, taxCode` từ `dbo.TCustomer` — form Tạo đơn điền sẵn các ô người gửi còn trống, khách vẫn sửa được) — chưa đăng nhập / hết phiên trả **401**. Trang ngoài (`/`, `/login`) và lớp chặn portal dựa vào endpoint này |

`SessionUser`: `{ customerCode, companyName, contactName?, email?, phone?, address?, taxCode?, avatarUrl?, defaultBranch?, userName, fullName, accountType: "customer"|"staff", isAdmin, roles, permissions }` — tài khoản con: `userName` / `fullName` của nhân viên, hồ sơ công ty của khách cha.

Người gửi trong payload đơn (`shipper`) có thêm `originalShipper` — tên shipper gốc khi khách là đơn vị forwarder gửi hộ, ghi vào `dbo.MaVanDon.Ten_Khach_Cua_FWD` khi in & cấp bill (tối đa 150 ký tự, không bắt buộc).

Khi `/me` trả 404/501 (backend chưa bật đăng nhập), frontend cho vào portal ở chế độ thử. Khi bật đăng nhập, mọi endpoint của portal nên trả 401 nếu không có phiên hợp lệ.

**Khớp với backend .NET (JWT):** backend phát JWT (`ICurrentUser` đọc claim `sub`, `customer_id`, `permission`…). Frontend **không** lưu token trong JavaScript — hãy đặt JWT vào cookie `httpOnly; Secure; SameSite=Lax; Path=/api` khi `POST /auth/login`, và cấu hình `JwtBearer` đọc token từ cookie trong `Events.OnMessageReceived` (vẫn nhận header `Authorization: Bearer` cho API Tracking / tích hợp ngoài). `remember: true` → cookie có `Expires` (VD 30 ngày); `false` → cookie phiên. `POST /auth/logout` xóa cookie. Nếu dùng refresh token thì cũng để trong cookie httpOnly riêng.

---

## 8. Trang ngoài (công khai, không cần đăng nhập) — `features/landing`

| Method | Path | Trạng thái | Mô tả |
|---|---|---|---|
| POST | `/public/tracking` | **Mới** | Tra cứu vận đơn trên trang chủ. Body `{ "bills": string[] }` (1–10 mã, đã viết hoa, bỏ trùng). Trả `{ data: TrackResult[] }` đúng thứ tự `bills` |
| POST | `/public/contact` | **Chưa làm** | Form "Gửi lời nhắn". Body `{ name, phone, email, message }` (`email` có thể rỗng). Trả `{ success, message }` — frontend hiện `message` lên toast |

`TrackResult` (xem `features/landing/types.ts`):

```json
{ "bill": "6156979", "found": true, "status": "fly", "service": "Chuyển phát nhanh", "destination": "Úc",
  "events": [{ "time": "29/09/2026 14:20", "title": "Đã bay", "location": "SGN" }] }
{ "bill": "VA999999", "found": false }
```

- `status` dùng chung mã với đơn hàng: `wait | fly | ok` (nhãn 1 đơn: có POD → ok, có ngày gửi → fly, còn lại → wait). `events` mới nhất trước.
- Tìm theo **mã bill Việt An hoặc mã hãng** (tracking DHL/FedEx/UPS/TNT). Chỉ trả thông tin hành trình — **không** trả tên/địa chỉ/điện thoại người gửi, người nhận, giá cước.
- Hai endpoint này mở công khai nên cần **giới hạn tần suất** theo IP (VD 30 lần/phút cho tracking, 5 lần/giờ cho contact) → trả **429** + `message` tiếng Việt. Validate lại phía server (tên ≤ 100, nội dung 10–2000 ký tự, số điện thoại 9–15 ký tự số).
- `/public/contact` lưu lại để nhân viên kinh doanh xử lý (và/hoặc gửi email thông báo nội bộ).

---

## Checklist cho backend SQL Server

1. Làm đủ các endpoint **Có sẵn** với đúng dạng dữ liệu trên (backend cũ chạy bằng dữ liệu in-memory — bản dự phòng giữ ngoài repo).
2. Làm các endpoint **Mới** — frontend đã gọi sẵn, chỉ cần trả đúng dạng.
3. Lưu `payload` của đơn nháp nguyên vẹn (JSON).
4. Trả lỗi có `message` tiếng Việt; endpoint chưa xong trả 404/501.
5. Chạy `npm run dev` trong `web/` (proxy `/api` → `http://localhost:3000`) để thử toàn bộ giao diện.
