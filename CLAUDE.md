# CLAUDE.md — Việt An Express Portal (frontend React + TypeScript)

> Hướng dẫn kỹ thuật cho AI Agent và lập trình viên. Đọc kỹ trước khi chỉnh sửa.

## 1. Tổng quan

Portal khách hàng của Việt An Express: tạo đơn, quản lý vận đơn, giá cước, e-commerce, sự cố, thông báo.
Repo gồm website trong `web/`, Zalo Mini App độc lập trong `mini-app/` (React 19 + TypeScript + Vite) và backend trong `backend/` (.NET 10, modular monolith, SQL Server database `vietan_app`). Backend phải tuân theo hợp đồng API trong `web/docs/API_CONTRACT.md`; kiến trúc, quy tắc module và cách chạy backend: `backend/README.md`.

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

**Database:** không tự tạo bảng / schema mới. Portal chỉ dùng bảng có sẵn (`dbo.TCustomer` cho đăng nhập và hồ sơ khách, `dbo.MaVanDon` + `MaVanDon_PCS_DIM` + `MaVanDon_ChiTietHang` cho vận đơn); ngoại lệ tạm giữ `shipments.OrderDrafts`; bảng mới đã được người dùng duyệt: `dbo.NhomHangHoa`, `dbo.MatHangKhachHang`, `dbo.KenhTMDT` + `dbo.KetNoiTMDT` + `dbo.DonTMDT` (kênh bán e-com, shop đã kết nối, đơn e-com), `dbo.MyTrackingCauHinh` (trang MyTracking của khách), `dbo.VungXauVungXa` (bảng vùng sâu vùng xa của hệ thống cũ, chỉ đọc — nạp từ script người dùng cung cấp), `dbo.GopY` + `dbo.GopY_HinhAnh` (góp ý khách gửi + ảnh, xem ở trang quản trị /admin), `dbo.ThongBao` + `dbo.ThongBao_DaDoc` (thông báo quản trị soạn ở /admin gửi khách + lượt đã đọc từng tài khoản; `ThongBao.CustomerID` có giá trị = thông báo riêng 1 khách), `dbo.SuCo` + `dbo.SuCo_TraoDoi` (sự cố khách báo cho vận đơn + lịch sử trao đổi, xử lý ở /admin) bảng tạm `dbo.TaiKhoanNhanVien` (tài khoản con của nhân viên — sau này ráp vào bảng chính thức) và `dbo.VanDonNguoiTao` (vận đơn do tài khoản con nào tạo). Cột portal thêm vào bảng cũ (người dùng đã duyệt): `dbo.MaVanDon.ConsigneeIossNo`, `ConsigneeEoriNo`, `VA_Bill` (mã bill hiển thị VAHCM6003585US); cột `dbo.GopY.Khao_Sat` (JSON câu trả lời khảo sát). Chức năng cần bảng mới thì hỏi người dùng trước.

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
- **Vòng đời đơn:** Tạo đơn → Đơn nháp & chưa in → In & cấp bill (backend cấp mã, khóa đơn) → Đơn hàng của tôi.
- **Import:** tạo đơn từ file Excel mẫu `Mau_Excel_Tao_Don.xlsx` (`order-import`, backend đọc và kiểm tra từng dòng), invoice từ CSV, CSV e-commerce 70 cột (gửi backend xử lý).

## 5. Quy ước bắt buộc

1. Chỉ gọi API trong `features/<x>/api.ts` qua `shared/api/http.ts`; thêm endpoint mới thì cập nhật `docs/API_CONTRACT.md`.
2. Không để dữ liệu giả trong frontend. Endpoint backend chưa có → xử lý bằng `isNotImplemented()`.
3. Màu, cỡ chữ, khoảng cách dùng biến trong `shared/styles/tokens.css`; màu chủ đạo xanh lá Việt An.
4. Không dùng emoji làm icon (`<Icon name>`); nhãn chữ thường, không viết HOA toàn bộ.
5. Logic tính toán viết thành hàm thuần trong `lib/` và có test.
