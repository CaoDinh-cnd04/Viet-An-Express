# Quy đổi và cân tính cước

Mọi hãng/tuyến giữ nguyên **Dài × Rộng × Cao (cm) ÷ 5.000**, theo xác nhận của người dùng.
Một dòng gồm các kiện giống nhau: `weight` là cân một kiện, `qty` là số lượng.
Cân tính cước = lấy max(cân thực, quy đổi) của một kiện, áp dụng làm tròn kiện nếu có,
nhân số lượng, cộng các dòng, rồi áp dụng làm tròn tổng lô nếu có.
Không lấy max(tổng cân thực, tổng cân quy đổi).

Ví dụ: kiện 20×30×40 cm / 6 kg → 6 kg; kiện 30×50×10 cm / 2 kg → 3 kg.
Cân thực tổng 8 kg, quy đổi 7,8 kg, cân tính cước 9 kg.

## Quy tắc

| Hãng/dịch vụ | Kích thước dùng để quy đổi | Làm tròn từng kiện | Làm tròn tổng |
|---|---|---|---|
| UPS Express / Saver (mặc định các hub UPS không có Expedited) | cm gần nhất; 0,5 lên | lên 0,5 kg | không thêm |
| UPS Expedited | cm gần nhất; 0,5 lên | lên 1 kg | không thêm |
| DHL Express | giữ kích thước nhập | lên 0,5 kg | đến 30 kg lên 0,5 kg; trên 30 kg lên 1 kg |
| FedEx IPE/IP/IE (mặc định hub Fedex không có FICP) | làm tròn lên cm | không thêm | đến 20,5 kg lên 0,5 kg; trên 20,5 kg lên 1 kg |
| FedEx FICP | làm tròn lên cm | không thêm | đến 32,5 kg lên 0,5 kg; trên 32,5 kg lên 1 kg |
| Các hãng/tuyến còn lại | giữ kích thước nhập | không thêm | không thêm |

Các hub đại lý chỉ có tên hãng/quốc gia nên áp dụng hồ sơ mặc định trên. Chưa có bảng
quy tắc riêng theo hợp đồng đại lý. Người dùng đã xác nhận không có hệ số quy đổi riêng.

Nguồn đối chiếu ngày 13/10/2026:

- [UPS Việt Nam: kích thước và cân tính cước](https://www.ups.com/vn/en/support/shipping-support/shipping-dimensions-weight).
- [DHL Việt Nam 2026, trang 15 và 26–28](https://mydhlplus.dhl.com/content/dam/downloads/vn/en/rate-guide/service_and_rate_guide_vn_en_2026.pdf.coredownload.pdf).
- [FedEx APAC: kích thước, trang 19](https://www.fedex.com/content/dam/fedex/apac-asia-pacific/downloads/fedex-apac-service-guide-vi-vn.pdf).
- [FedEx Việt Nam 2026: phụ phí/thông tin trọng lượng, trang 4](https://www.fedex.com/content/dam/fedex/international/rates/fedex-rates-sur-vi-vn-2026.pdf).
- [FedEx IPE/IP/IE 2026: mốc giá](https://www.fedex.com/content/dam/fedex/international/rates/fedex-rates-exp-en-vn-2026.pdf).
- [FedEx FICP 2026: mốc giá](https://www.fedex.com/content/dam/fedex/international/rates/fedex-rates-ficp-exp-vi-vn-2026.pdf).

Đây là phép tính cân cho bao bì khách tự đóng hiện có trên form. Mức cân tối thiểu do bao bì
FedEx riêng, hàng quá khổ, AHS, pallet/freight và hợp đồng riêng không nằm trong phép tính
cân thông thường này; việc xét phụ phí/giới hạn hãng là phần riêng.

## Đồng bộ dữ liệu

- Frontend: `shared/lib/shipping-weight.ts`; backend: `Domain/ShippingRules.cs`.
  Khi đổi quy tắc phải cập nhật cả hai và bộ kiểm thử tương ứng.
- Tổng form, nhãn đơn nháp, chi tiết nháp, cân gửi tra giá, backend cấp bill và preview Excel
  cùng dùng quy tắc theo hãng/hub. Endpoint báo giá hiện vẫn nhận một cân và một bộ kích thước;
  tạo đơn gửi cân tính cước đã tính của hãng đã chọn, không gửi tổng cân thực thay thế.
  Bộ kích thước của kiện có cạnh lớn nhất vẫn dùng cho kiểm tra phụ phí hiện có;
  đây không phải báo giá đầy đủ phụ phí của mọi kiện.
- Khi lưu vào bảng kiện cũ, lưu tổng cân thực/quy đổi/tính cước theo dòng. DTO chi tiết đơn
  trả thêm `volumetricWeight`, `chargeableWeight` theo dòng và `chargeableWeight` tổng đơn.
  Xem/in ưu tiên số đã lưu vì cột kích thước database cũ chỉ nhận số nguyên.
- Không tính lại hay cập nhật vận đơn đã cấp bill. Cân tổng đơn đã lưu được ưu tiên khi xem.
- Không thêm bảng hoặc migration.

Kiểm thử có ví dụ hai kiện, số lượng > 1, các ngưỡng làm tròn, số thập phân sát ngưỡng,
đổi hub Express/Expedited/FICP, giữ cân đã lưu khi xem/in và tính import Excel.
