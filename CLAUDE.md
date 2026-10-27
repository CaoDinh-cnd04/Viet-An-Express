# CLAUDE.md — Việt An Express Portal (frontend React + TypeScript)

> Hướng dẫn kỹ thuật cho AI Agent và lập trình viên. Đọc kỹ trước khi chỉnh sửa.

## 1. Tổng quan

Portal khách hàng của Việt An Express: tạo đơn, quản lý vận đơn, giá cước, e-commerce, sự cố, thông báo.
Repo gồm website trong `web/`, Zalo Mini App độc lập trong `mini-app/` (React 19 + TypeScript + Vite) và backend trong `backend/` (.NET 10, modular monolith, SQL Server database `vietanexpress`). Backend phải tuân theo hợp đồng API trong `web/docs/API_CONTRACT.md`; kiến trúc, quy tắc module và cách chạy backend: `backend/README.md`.

Ngôn ngữ giao tiếp & giao diện: **Tiếng Việt**.

## 2. Cấu trúc

```
web/
├── docs/API_CONTRACT.md   # Hợp đồng API — nguồn chuẩn cho backend
├── public/templates/      # File mẫu import (CSV/XLSX 70 cột e-commerce)
└── src/
    ├── app/               # Router, provider, layout (sidebar, topbar, menu)
    ├── features/<x>/      # Mỗi chức năng: api.ts, types.ts, constants.ts, schema.ts, lib/, hooks/, components/, pages/, index.ts
    └── shared/            # api/http.ts, config/domain.ts, lib/, styles/ (tokens), ui/ (component dùng chung)
```

Chi tiết quy ước code và cách thêm trang: `web/README.md`.

Mini App là project riêng có `package.json`, lockfile, assets và cấu hình trong `mini-app/`; cấu trúc `src/app`, `src/features`, `src/shared` tuân theo cùng kiến trúc module của web. Không import mã từ `web/`. Quy ước: `mini-app/AGENTS.md`; cách chạy: `mini-app/README.md`. Chạy `npm.cmd ci`, `npm.cmd run dev`, `npm.cmd test`, `npm.cmd run build` trong thư mục `mini-app/`.

```
backend/
├── src/VietAnExpress.API/                  # Host (cổng 3000 khi dev)
├── src/BuildingBlocks/VietAnExpress.SharedKernel/
├── src/Modules/{Identity,Customers,Shipments,Ecommerce}/   # mỗi module + .Contracts; dùng thẳng bảng dbo có sẵn
└── tests/                                  # xUnit v3 + Moq, Architecture.Tests kiểm tra ranh giới module
```

Backend: `cd backend && dotnet run --project src/VietAnExpress.API --launch-profile http` · test: `dotnet test --solution VietAnExpress.slnx`. Bí mật (connection string, `Jwt:Secret`, `Email:Password` — mật khẩu ứng dụng Gmail gửi thông báo / nhận góp ý) chỉ để trong user-secrets / biến môi trường.

**Database:** không tự tạo bảng / schema mới. Portal chỉ dùng bảng có sẵn (`dbo.TCustomer` cho đăng nhập và hồ sơ khách, `dbo.MaVanDon` + `MaVanDon_PCS_DIM` + `MaVanDon_ChiTietHang` cho vận đơn); bảng mới đã được người dùng duyệt: `dbo.NhomHangHoa`, `dbo.MatHangKhachHang`, `dbo.KenhTMDT` + `dbo.KetNoiTMDT` + `dbo.DonTMDT` (kênh bán e-com, shop đã kết nối, đơn e-com), `dbo.TCustomer_MyTracking` + `dbo.TCustomer_MyTracking_Banner` (trang MyTracking của khách; đã chuyển cấu hình và ảnh từ MyTrackingCauHinh, bảng cũ portal đã bỏ), `dbo.VungXauVungXa` (bảng vùng sâu vùng xa của hệ thống cũ, chỉ đọc — nạp từ script người dùng cung cấp), `dbo.TThongBao` + `dbo.TThongBao_DaDoc` (thông báo quản trị soạn ở /admin gửi khách + lượt đã đọc từng tài khoản; 27/10/2026 gộp 4 bảng ThongBao / ThongBao_Tin / ThongBao_NguoiNhan / ThongBao_DaDoc về 2 bảng, migration `MergeNotificationTables`). `TThongBao.CustomerID` có giá trị = thông báo riêng 1 khách (cập nhật hỗ trợ & sự cố); các tin nằm ở cột JSON `Tin` (`[{ Thu_Tu, Tieu_De, Noi_Dung, Chi_Tiet, Hinh_Anh, Duong_Dan }]` — `Noi_Dung` chữ thường hoặc HTML có định dạng đã lọc, ảnh `Hinh_Anh` = đường dẫn `/Upload/ThongBao/…`, nút "Xem chi tiết" mở `Duong_Dan` hoặc `Chi_Tiet` — chỉ 1 trong 2); khách chỉ định ở cột `Khach_Nhan` dạng ",201008,200877,"; cột `Ghim`, `Ngay_Dang` (hẹn giờ), `Ngay_Het_Han`, `Trang_Thai` (0 nháp · 1 đã đăng · 2 đã gỡ), `Doi_Tuong` (`all` / `type` theo `TCustomer.CustomerTypeID` (tên loại ở bảng cũ `dbo.TCustomerType`, chỉ đọc: 1 Công ty · 2 Cá nhân) ở `Loai_Khach` dạng ",1,2," / `pick` theo `Khach_Nhan`). Bảng `dbo.PhienThuHoi` (thu hồi phiên khi đăng xuất) đã bỏ 21/10/2026; từ 27/10/2026 đăng xuất thu hồi token không cần bảng — danh sách jti đã thu hồi ở bộ nhớ + file `App_Data/revoked-tokens.json` của backend (pool IIS cần quyền ghi thư mục này), phiên tự gia hạn tối đa `Jwt:MaxSessionDays` (14) ngày từ lần nhập mật khẩu, access token 5 phút. Vận đơn do tài khoản con nào tạo ghi ở cột có sẵn `MaVanDon.Customer_Staff_ID` (= `TCustomer_NguoiLienLac.ID`, 0 = tài khoản chính / hệ thống cũ; bảng phụ `VanDonNguoiTao` đã bỏ 16/10/2026). Bảng người dùng tự tạo và nạp dữ liệu, portal chỉ đọc (map `ExcludeFromMigrations`): `dbo.Catalog_HsCode` (danh mục mã HS theo nước nhập khẩu). Bảng hệ thống cũ người dùng cung cấp script, nạp bằng `backend/scripts/legacy-tables.sql`: `dbo.TCustomer_NguoiLienLac` (người liên hệ của khách — portal dùng làm tài khoản con, xem dưới), `dbo.TContact` (Hỗ trợ & sự cố của portal: dòng `FormType` = "Portal - Hỗ trợ" (hỗ trợ & sự cố gộp 1 từ 27/10/2026, mã vận đơn không bắt buộc; dòng cũ "Portal - Sự cố") / "Portal - Góp ý" (`KeyWord` "Feedback", `Title` = loại góp ý — giống form "Yêu cầu" Bill Online cũ) / "Portal - Liên hệ" (form "Gửi lời nhắn" trang chủ, khách vãng lai, `CustID` 0, `KeyWord` "ContactHome" như form website cũ), `Status` 0 mới · 1 đang xử lý · 2 chờ khách · 3 đã xử lý, `Des01` vận đơn · `Des02` mức độ · `Des03` người nhận · `Des04` nước · `Des05` ID tài khoản con, `KeyWord` loại / chủ đề; không có trao đổi qua lại, tệp đính kèm chỉ gửi email; sửa thông tin người gửi là yêu cầu hỗ trợ thường — CS sửa ở phần mềm cũ), `dbo.TCustomer_MyTracking` + `dbo.TCustomer_MyTracking_Banner` (MyTracking của hệ thống cũ), `dbo.Countries` (290 nước / mã đặc biệt kèm mã điện thoại; đã dùng để tra `CountryID` ghi vào `MaVanDon.ConsigneeCountryID` khi cấp bill / import, chỉ đọc SQL trực tiếp). Portal **không** đọc / ghi `dbo.Order_Trouble` + `Order_Trouble_Detail`: đó là sổ theo dõi đơn trễ nội bộ của nhân viên (proc `p_Order_Trouble_Rows` tự thêm đơn quá `EstPOD` ≥ 3 ngày, tự đóng khi có `BOD`; `Trang_Thai` 1, 2 chưa xong · 3 xong) — phía khách của hệ thống cũ chỉ có form "Yêu cầu" ghi `TContact`. Bảng phía CMS / nhân viên của hệ thống cũ (chỉ cấu trúc, script `backend/scripts/legacy-support-tables.sql` tách từ dump schema `backup/vietan.sql` 27/10/2026): `dbo.Orders` (vận đơn sau khai thác / tính cước), `Order_Trouble` + `Order_Trouble_Detail` (sự cố / đơn trễ + trao đổi) — SQL laptop chỉ giữ 3 bảng portal dùng này; `Order_Tracking`, `Claim`, `TReply`, `TCommonFile`, `Carriers`, `Suppliers` portal không dùng, đã xóa khỏi laptop 27/10/2026 (cần thì nạp lại từ script). Tài khoản con của nhân viên = người liên hệ có tên đăng nhập trong bảng cũ `dbo.TCustomer_NguoiLienLac` (`TinhCach` = tên đăng nhập, `Mo_Ta` = mật khẩu lưu như hệ thống cũ, `Trang_Thai` 1 = hoạt động; xóa tài khoản chỉ gỡ đăng nhập). Cột portal thêm vào bảng cũ (người dùng đã duyệt): `dbo.TCustomer_NguoiLienLac.Quyen` (quyền tài khoản con), `dbo.MaVanDon.ConsigneeIossNo`, `ConsigneeEoriNo`, `VA_Bill` (từ 23/10/2026 portal không ghi nữa, để NULL như hệ thống cũ — mã bill hiển thị là `OrderNumber`), `dbo.MaVanDon_ChiTietHang.Kien` (số kiện của dòng hàng khi tuyến khai invoice riêng từng kiện — Chuyên tuyến Úc / EU / Canada, danh sách hub ở `PER_PACKAGE_INVOICE_HUBS`; NULL = invoice chung). Chức năng cần bảng mới thì hỏi người dùng trước.

## 3. Lệnh thường dùng (chạy trong `web/`)

```bash
npm install
npm run dev        # http://localhost:5173, proxy /api → http://localhost:3000 (backend)
npm run typecheck
npm test           # vitest
npm run build      # xuất web/dist
```

## 4. Quy tắc nghiệp vụ đã triển khai ở frontend

- **DOC sang PACK:** chứng từ > 2kg tự chuyển sang hàng hóa (`create-order/lib/shipment.ts`, `RULES.docMaxWeightKg`).
- **Quy đổi thể tích:** cân tính cước = `max(cân thực, D×R×C / 5000)`.
- **Giới hạn kích thước theo hãng:** cảnh báo quá khổ / quá tải, chặn kiện hãng không nhận (`create-order/lib/carrier-limits.ts`).
- **Vòng đời đơn:** Tạo đơn → Đơn nháp & chưa in → In & cấp bill (backend cấp mã, khóa đơn) → Đơn hàng của tôi. Đơn chưa in nằm luôn trong `dbo.MaVanDon` với `Status = 0` (chưa có `OrderNumber`; `VA_Bill` NULL — không lưu chi nhánh, mở lại nháp mặc định TP.HCM), in xong `Status = 1`. Chỉ lưu thông tin có cột trong MaVanDon + 2 bảng chi tiết (người dùng chấp nhận mất dịch vụ cộng thêm, DDU/DDP… khi mở lại nháp). Mọi truy vấn đơn khác tự bỏ `Status = 0` (query filter của `LegacyOrder`); hệ thống cũ cũng hiện đơn này và cho sửa / xóa.
- **Tự đăng xuất khi không thao tác:** 30 phút không chuột / phím / chạm / cuộn (tính chung mọi tab, `auth/lib/idle.ts`, `IdleLogout` trong AppShell) thì đăng xuất luôn, không hỏi trước. Trang Tạo / sửa đơn có thay đổi chưa lưu thì lưu vào Đơn nháp trước (`onBeforeIdleLogout`), đăng nhập lại mở /drafts; trang khác đăng nhập lại về đúng trang đang xem.
- **Import:** tạo đơn từ file Excel mẫu `Mau_Excel_Tao_Don.xlsx` (`order-import`, backend đọc và kiểm tra từng dòng), invoice từ CSV, CSV e-commerce 70 cột (gửi backend xử lý).

## 5. Quy ước bắt buộc

1. Chỉ gọi API trong `features/<x>/api.ts` qua `shared/api/http.ts`; thêm endpoint mới thì cập nhật `docs/API_CONTRACT.md`.
2. Không để dữ liệu giả trong frontend. Endpoint backend chưa có → xử lý bằng `isNotImplemented()`.
3. Màu, cỡ chữ, khoảng cách dùng biến trong `shared/styles/tokens.css`; màu chủ đạo xanh lá Việt An.
4. Không dùng emoji làm icon (`<Icon name>`); nhãn chữ thường, không viết HOA toàn bộ.
5. Logic tính toán viết thành hàm thuần trong `lib/` và có test.

CMS (trang tổng của nhân viên Việt An, đăng nhập `dbo.USERS`) sẽ chạy song song trên cùng database: luồng nhân viên phụ trách khách (`TCustomer.Staff_ID` → `USERS`) và quy ước dữ liệu portal ghi xem "backend/docs/CMS_NHAN_VIEN_PHU_TRACH.md".

MyTracking chỉ tài khoản chính vào được và chỉ khi CMS bật `dbo.TCustomer.ViewLinkTracking = 1` (portal chỉ đọc cột này; tài khoản con không bao giờ vào được, trang công khai không phụ thuộc). MyTracking dùng hai bảng hệ thống cũ: ánh xạ cột, cột bổ sung và chuyển dữ liệu xem "backend/docs/MYTRACKING_STORAGE.md".
