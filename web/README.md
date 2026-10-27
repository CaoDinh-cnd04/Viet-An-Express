# Việt An Express Portal — Web (React + TypeScript)

Giao diện Zalo Mini App là project độc lập trong [`../mini-app/`](../mini-app/README.md). Chạy lệnh Mini App trong thư mục đó; thư mục `web/` chỉ chứa website.

Giao diện portal khách hàng. Toàn bộ dữ liệu lấy từ backend qua REST API — **frontend không chứa dữ liệu giả**.
Hợp đồng API: [docs/API_CONTRACT.md](docs/API_CONTRACT.md).

## Chạy

```bash
npm install
npm run dev        # http://localhost:5173 — proxy /api sang backend http://localhost:3000
npm run typecheck  # kiểm tra kiểu
npm test           # unit test (vitest)
npm run build      # xuất ra web/dist
```

Biến môi trường: `VITE_API_BASE_URL` (mặc định `/api/v1`).

## Công nghệ

React 19 · React Router 7 · TanStack Query 5 (dữ liệu server) · React Hook Form + Zod (form & kiểm tra) · CSS Modules + design tokens · Vitest.

## Cấu trúc thư mục

```
src/
├── app/                  # Khung ứng dụng: router, provider, layout (sidebar, topbar)
│   ├── routes.tsx        # Khai báo trang
│   └── layout/nav.config.ts  # Menu sidebar
├── features/             # Mỗi chức năng một thư mục, độc lập nhau
│   └── <feature>/
│       ├── api.ts        # Hook gọi API (useQuery / useMutation) — nơi DUY NHẤT gọi http
│       ├── types.ts      # Kiểu dữ liệu khớp backend
│       ├── constants.ts  # Nhãn, trạng thái, danh sách tĩnh
│       ├── schema.ts     # (nếu có form) Zod schema = kiểu + quy tắc kiểm tra
│       ├── lib/          # Hàm thuần (tính toán, đọc file) + *.test.ts
│       ├── hooks/        # Hook riêng của feature
│       ├── components/   # Component riêng của feature
│       ├── pages/        # Trang (default export, lazy-load)
│       └── index.ts      # API công khai cho feature khác import
└── shared/               # Dùng chung, không phụ thuộc feature
    ├── api/http.ts       # HTTP client, ApiError, getErrorMessage
    ├── config/domain.ts  # Hằng số nghiệp vụ chung (chi nhánh, hãng/hub, quốc gia)
    ├── lib/              # format số/ngày, file CSV, hook tiện ích
    ├── styles/           # tokens.css (màu, cỡ chữ, khoảng cách) + global.css
    └── ui/               # Component giao diện: Button, Card, DataTable, Modal, TextField…
```

| Feature | Trang | Route |
|---|---|---|
| landing | Trang ngoài: giới thiệu, tra cứu vận đơn, liên hệ (không cần phiên) | `/`, `/login?next=`, `/?track=MA1,MA2` |
| auth | Đăng nhập, phiên, chặn trang portal; `/home` chuyển tới Tạo đơn (không có quyền tạo đơn → Đơn hàng của tôi) | `/home` |
| create-order | Tạo đơn từng bước / 1 trang | `/orders/new`, `/orders/new/quick` |
| order-import | Tạo đơn từ Excel | `/orders/import` |
| orders | Đơn hàng của tôi | `/orders` |
| drafts | Đơn nháp & chưa in | `/drafts` |
| pricing | Giá & gợi ý dịch vụ | `/pricing?tab=lookup\|tables\|manage` |
| ecommerce | Kênh bán hàng | `/ecommerce?tab=overview\|push\|orders\|conn` |
| troubles | Quản lý sự cố | `/troubles` (`?bill=` mở sẵn hộp báo sự cố) |
| notifications | Thông báo (+ popup quan trọng) | `/notifications` |
| support | Trợ giúp & Góp ý | `/help` |
| account | API Tracking, Đổi mật khẩu | `/account/api-tracking`, `/account/password` |
| mytracking | MyTracking cá nhân (admin), trang tra cứu công khai | `/account/mytracking`, `/t/:slug` |
| staff | Tài khoản nhân viên (tài khoản con, phân quyền — chỉ admin) | `/account/staff` |

## Quy ước code

1. **Gọi API chỉ trong `features/<x>/api.ts`**, qua `http` của `shared/api`. Component không gọi `fetch`.
2. **Feature không import sâu vào feature khác** — chỉ import từ `@/features/<x>` (file `index.ts`). Ngoại lệ đang có: `orders/api`, `orders/constants`, `drafts/api` (dùng chung rộng rãi).
3. **Không viết mã màu, cỡ chữ cứng** — dùng biến trong `shared/styles/tokens.css`. Không dùng `style={{…}}` trong JSX, trừ giá trị tính lúc chạy (độ rộng cột, vị trí menu); còn lại viết CSS Module.
4. **Không dùng emoji làm icon** — dùng `<Icon name="…" />` (thêm icon mới ở `shared/ui/Icon.tsx`).
5. **Form**: React Hook Form + Zod; schema là nguồn duy nhất cho kiểu & quy tắc.
6. **Logic tính toán** (cước, quy đổi, đọc CSV, giới hạn hãng) viết thành hàm thuần trong `lib/` và có test.
7. Chữ hiển thị tiếng Việt, **chữ thường** (không viết HOA toàn bộ nhãn / tiêu đề cột).
8. Endpoint backend chưa làm → xử lý `isNotImplemented(error)` để hiện "đang kết nối máy chủ", không làm vỡ trang.

## Thêm một trang mới

1. Tạo `src/features/<ten>/pages/<Ten>Page.tsx` (default export).
2. Khai báo trong `src/app/routes.tsx`: thêm vào `pages` và 1 dòng `page('duong-dan', 'Tiêu đề', pages.ten)`.
3. Thêm mục menu trong `src/app/layout/nav.config.ts`.
4. Viết `api.ts` + `types.ts`, và bổ sung endpoint vào `docs/API_CONTRACT.md`.
