# Chức năng tạm ẩn — chờ hệ thống cũ xác nhận

Cập nhật 27/10/2026. **Hiện không còn chức năng nào tạm tắt.** Các chức năng dưới đây (nếu có) đã code xong, chạy được trên laptop, nhưng **ghi vào bảng của hệ thống cũ** theo quy ước dev hệ thống cũ chưa xác nhận. Bản đẩy lên máy chủ (FTP) để **tắt**; dev đồng ý thì bật lại, không cần sửa code.

| # | Chức năng | Ghi vào bảng cũ | Khi tắt thì portal làm gì | Bật lại ở đâu |
|---|---|---|---|---|

Trên laptop (`appsettings.Development.json`) công tắc đang **bật** để thử; `appsettings.json` (máy chủ) **tắt**.

## Câu hỏi chờ dev hệ thống cũ trả lời trước khi bật

(Dump 27/10/2026: form trang chủ website cũ lưu `TContact.KeyWord = 'ContactHome'`, màn CS là proc `p_TContactHome_Rows`.)

1. ~~Form liên hệ website ghi gì~~ — đã biết từ dữ liệu thật: `KeyWord` "ContactHome", `Title` NULL, `FormType` NULL; portal đã ghi giống (`FormType` "Portal - Liên hệ" để biết nguồn). Còn lại: dev đồng ý thì bật.

Đã tự xác nhận từ dump `backup/vietan.sql` (27/10/2026): `Orders` nối `MaVanDon` bằng `cast(MaVanDon.OrderNumber as nvarchar(20)) = Orders.OrderNumber` (proc `A_Order_Update_Reweight_Tang`…); dòng `Orders` do nhân viên nhập lên hệ thống (proc `A_Order_Create`), không có trigger tự chép từ MaVanDon → bill mới cấp trên portal chưa có trong `Orders` ngay.

## Không ảnh hưởng hệ thống cũ (vẫn đẩy lên bình thường)

- Bảng thông báo của portal (`TThongBao`, `TThongBao_DaDoc`) và xoá `ThongKeSuDung` — bảng riêng của portal, dev chạy 2 file SQL trong `publish/migrations` trước khi đẩy backend.
- Yêu cầu hỗ trợ / góp ý / sự cố đơn đã giao vào `TContact` ("Portal - …") — đã chạy từ trước.
- Tự lưu đơn đang tạo vào Đơn nháp khi tự đăng xuất (`MaVanDon.Status = 0`, như nút Lưu nháp — người dùng xác nhận không cần tắt).
- Bảng Hỗ trợ & sự cố ở /admin, lọc nhân viên (chỉ **đọc** `TCustomer.Staff_ID` → `USERS`).
- Đăng xuất thu hồi token (file `App_Data`), phiên tối đa 14 ngày, tự đăng xuất 30 phút, khoá tạm khi nhập sai mật khẩu, chặn iframe.

## Hỗ trợ & góp ý của khách đăng nhập (27/10/2026)

- **Góp ý** = form "Yêu cầu" của Bill Online cũ, ghi giống hệt theo dữ liệu thật (ảnh SSMS 27/10/2026): `KeyWord` = "Feedback", `Title` = loại góp ý ("Chỉnh sửa phần mềm" / "Cập nhật thông tin" / "Góp ý nâng cấp" / "Khác"), `Content` = nội dung. Form cũ để `FormType` NULL; portal ghi "Portal - Góp ý" để biết nguồn (proc cũ không lọc theo cột này).
- **Hỗ trợ & sự cố** (sự cố vận đơn, tracking, cước, tạo đơn, sửa thông tin…) là chức năng **mới**, hệ thống cũ không có form tương ứng (khách vẫn gọi điện / Zalo). Lưu `TContact` FormType "Portal - Hỗ trợ", `KeyWord` = vấn đề, `Des01` = mã vận đơn (nếu có), `Des02` = mức độ. **CS cần biết:** xem / xử lý ở tab Hỗ trợ & sự cố của /admin (đổi trạng thái → khách nhận thông báo); màn liên hệ khách của phần mềm cũ (`p_TContact_Rows`) chỉ hiện các dòng này khi lọc `KeyWord` trống.

## Đã bỏ (27/10/2026)

- Sự cố `SC-…` nhắn qua lại ghi `dbo.Order_Trouble` / `Order_Trouble_Detail`: đọc dump thấy 2 bảng này là sổ theo dõi đơn trễ nội bộ của nhân viên (tự sinh / tự đóng), không phải nơi khách gửi yêu cầu — đã gỡ code. Hỗ trợ & sự cố gộp 1, lưu `TContact` như form "Yêu cầu" của Bill Online cũ.
- Form "Gửi lời nhắn" trang chủ: **đã bật** (27/10/2026, người dùng đồng ý) sau khi ghi giống hệt form liên hệ website cũ (`KeyWord` "ContactHome", `Title` trống, `Des02` NULL, `Gender` 1, `CustID` 0) — đã gỡ công tắc `Features:PublicContact`.
