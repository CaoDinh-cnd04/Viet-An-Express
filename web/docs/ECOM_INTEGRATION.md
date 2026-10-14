# Kết nối sàn E-commerce: TikTok Shop

> Tích hợp Shopify (OAuth, webhook, nhập file export) đã được gỡ khỏi portal tháng 10/2026; danh mục `dbo.KenhTMDT` vẫn giữ dòng `shopify` để khớp dữ liệu cũ.

> Tìm hiểu tháng 10/2026, dùng làm cơ sở cho backend `/ecom/stores/*` (hợp đồng API: `API_CONTRACT.md` §5.1).
> Các mốc phiên bản / chính sách của sàn thay đổi theo quý, kiểm lại tài liệu gốc trước khi code.

## 1. Luồng chung

```
Khách bấm "Kết nối"  →  POST /ecom/stores/connect  →  backend tạo state, trả authorizeUrl
        →  trình duyệt sang trang ủy quyền của sàn  →  chủ shop đồng ý
        →  sàn redirect về  GET /ecom/oauth/{platform}/callback?code&state…
        →  backend kiểm state, đổi code lấy token, lưu token (mã hóa), đăng ký webhook
        →  302 về /ecommerce?tab=connect&connected={platform}

Đơn mới   : sàn → POST /ecom/webhook/{platform} → hàng đợi → gọi API sàn lấy đơn đầy đủ → đơn E-commerce (src = platform)
Dự phòng  : job định kỳ (VD 15 phút) kéo đơn từ lastSyncAt, bù webhook bị lỡ
Cấp bill  : khi in & cấp bill (hoặc trạng thái "departed") → luôn đẩy tracking lên sàn
Thu hồi   : sự kiện hủy ủy quyền của sàn → status = "revoked"
```

Nguyên tắc: phản hồi webhook nhanh (200 ngay, xử lý nền), chống trùng theo id sự kiện, mọi lần gọi sàn đều idempotent theo mã đơn sàn (`ref`), token chỉ ở backend.

## 2. TikTok Shop

| Mục | Ghi chú |
|---|---|
| Đăng ký | Tài khoản developer trên **TikTok Shop Partner Center**, tạo app (loại ERP / logistics) → `app_key`, `app_secret`, `service_id`. Cần duyệt app và xin từng nhóm quyền (Order, Fulfillment, Logistics) |
| Ủy quyền | Chuyển chủ shop tới trang ủy quyền dịch vụ (`services.tiktokshop.com/open/authorize?service_id=…&state=…`; US dùng cổng riêng `services.us.tiktokshop.com`). Redirect về với **`code`** — sống **30 phút, dùng 1 lần** |
| Token | `GET https://auth.tiktok-shops.com/api/v2/token/get?app_key&app_secret&auth_code&grant_type=authorized_code` → `access_token`, `access_token_expire_in`, `refresh_token`, `refresh_token_expire_in`. Làm mới: cùng endpoint `/api/v2/token/refresh`, `grant_type=refresh_token`. Lên lịch refresh trước hạn |
| Shop | Sau khi có token gọi `GET /authorization/202309/shops` lấy `shop_cipher` + region của từng shop — mọi API theo shop đều cần `shop_cipher` |
| Gọi API | Host `open-api.tiktokglobalshop.com`, phiên bản `202309` (hoặc mới hơn). Query bắt buộc `app_key, timestamp, sign, shop_cipher`; header `x-tts-access-token` |
| Chữ ký | `sign` = hex(HMAC-SHA256(app_secret, app_secret + path + các tham số query đã sắp xếp theo tên nối "keyvalue" (bỏ `sign`, `access_token`) + body JSON + app_secret)) |
| Lấy đơn | `POST /order/202309/orders/search` (lọc `order_status = AWAITING_SHIPMENT`, theo `update_time`), chi tiết `GET /order/202309/orders?ids=` |
| Đẩy tracking | Chỉ áp dụng đơn **người bán tự vận chuyển** (seller shipping). API **Mark Package As Shipped** (Fulfillment) gửi `tracking_number` + `shipping_provider_id` (lấy từ *Get Shipping Providers*). Hiện chỉ mở cho **US, UK, ES, IE**; tracking không được chứa `/`, `-`, khoảng trắng |
| Webhook | Khai URL trong Partner Center, chọn sự kiện (đổi trạng thái đơn, hủy đơn, thu hồi ủy quyền…). Kiểm header `Authorization` = hex(HMAC-SHA256(app_secret, app_key + body thô)) |

### Rủi ro cần chốt với kinh doanh

1. **Hãng vận chuyển phải nằm trong danh sách TikTok cho phép.** Việt An không có trong danh sách thì phải đẩy **mã hãng chặng cuối** (DHL / FedEx / UPS / USPS…) thay vì mã bill Việt An → tracking chỉ đẩy được sau khi có mã hãng.
2. Shop dùng **TikTok Shipping** (sàn chỉ định vận chuyển) thì không đẩy tracking được, chỉ nên nhận đơn.
3. Shop TikTok Việt Nam nội địa gần như luôn dùng vận chuyển của sàn — đối tượng chính là người bán xuyên biên giới bán sang US / UK / EU.
4. Duyệt app TikTok mất vài tuần, cần làm sớm.

## 3. Backend

**Đã có** (module `Ecommerce`, migration `AddEcommerceChannels`):

| Bảng | Nội dung |
|---|---|
| `dbo.KenhTMDT` | Danh mục kênh bán: `Ma_Kenh` (shopify, tiktok, amazon, ebay, etsy, woocommerce, shopee, lazada), tên, kiểu kết nối (`oauth2` / `api_key`), website, `Dang_Hoat_Dong` (portal chưa có adapter kênh nào — `stores/connect` luôn báo chưa hỗ trợ), `Day_Tracking`, thứ tự |
| `dbo.KetNoiTMDT` | Shop của khách: `CustomerID`, `Ma_Kenh` (FK), `Ma_Shop`, tên / tên miền shop, `Shop_Cipher`, khu vực, tiền tệ, quyền, access/refresh token **mã hóa** + hạn, id webhook, `Trang_Thai`, ngày kết nối / đồng bộ / lỗi / ngắt kết nối |

**Còn cần** (chưa làm — cần duyệt bảng mới):

| Bảng đề xuất | Nội dung |
|---|---|
| `EcomOAuthState` | `state` ngẫu nhiên, khách, sàn, hạn 10 phút (có thể thay bằng cache phân tán nếu có) |
| `EcomWebhookEvent` | Id sự kiện + thời điểm — chống xử lý trùng, giữ ~7 ngày |
| `EcomOrderLink` | Mã đơn sàn ↔ đơn nháp / `MaVanDon`, trạng thái đẩy tracking (để thử lại khi lỗi) |

Module `Ecommerce` đã có đơn nhập tay, sửa / xác nhận / xóa đơn và danh sách shop; tiếp theo client TikTok là adapter trong Infrastructure; job nền (hosted service) cho refresh token, đồng bộ dự phòng và hàng đợi đẩy tracking có retry. Secret (`client_secret`, `app_secret`, khóa mã hóa token) đặt trong user-secrets / biến môi trường.

## Nguồn

- TikTok Shop — [Partner Center](https://partner.tiktokshop.com/docv2), [Mark Package As Shipped (tóm tắt)](https://mindcloud.co/docs/universal/rest/tiktok-shop/latest/actions/mark-package-as-shipped), [Hãng vận chuyển chính thức US](https://help.m2epro.com/en/support/solutions/articles/9000239519)
