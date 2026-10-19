# Đối chiếu cấu trúc và quy tắc đặt tên database — 15/10/2026

## Kết luận

**Chưa đồng nhất hoàn toàn với hệ thống cũ.** Tên bảng và cột trong code hiện khớp với database đang kiểm tra, nhưng quy tắc đặt tên giữa các nhóm bảng còn khác nhau. Ngoài tên gọi, có 8 khai báo kiểu / NULL chưa khớp database và một mã quốc gia bị trùng.

Lượt kiểm tra này chỉ đọc database, không đổi tên bảng, cột hoặc dữ liệu.

## Phạm vi và bằng chứng

- Database thực tế: `vietanexpress` trên instance được cấu hình cho ứng dụng, `LAPTOP-K91T0OHE\SQLEXPRESS01`.
- Đọc `sys.tables`, `sys.columns`, `sys.types`, `sys.foreign_keys`, `sys.foreign_key_columns` và lịch sử migration của 3 module.
- Database có **30 bảng / 538 cột**: 26 bảng nghiệp vụ, 3 bảng lịch sử migration, 1 bảng `sysdiagrams`.
- Xuất mô hình EF hiện tại của 4 module: **20 bảng / 308 cột khác nhau**, tương ứng 309 khai báo cột giữa các module do `TCustomer.CustomerID` được dùng ở hai module.
- Không thiếu bảng hoặc cột nào trong 309 khai báo đã đối chiếu. Ngoài mô hình EF, code còn đọc trực tiếp `Countries` và `VungXauVungXa` bằng SQL.
- Snapshot EF khớp mô hình hiện tại; lịch sử migration SQL khớp danh sách trong code: Identity 14, Shipments 12, Ecommerce 7.
- Kiểm tra 25 liên kết ID bằng truy vấn đếm bản ghi không có bản ghi cha: **0 lỗi**. Kiểm tra thêm quyền sở hữu khách hàng của tài khoản con trong 5 bảng: **0 trường hợp lệch khách**.
- Chi tiết từng khai báo cột: [schema-audit-2026-10-15.csv](schema-audit-2026-10-15.csv).

Các kết quả trên chỉ áp dụng database và code tại thời điểm kiểm tra. Không bao gồm database triển khai khác, trigger / stored procedure của phần mềm cũ hoặc mọi đường ghi dữ liệu ngoài portal.

## 1. Những điểm chưa thống nhất về tên

Hệ thống cũ có nhiều kiểu đặt tên: `TCustomer`, `TCustomer_NguoiLienLac`, `TCustomer_MyTracking`, `MaVanDon`, `MaVanDon_PCS_DIM`, `Countries`. Cột cũng có cả `CustomerID`, `MaVanDonID`, `Create_Date`, `ModifyDate`, `Ten_hang`. Vì vậy, không có căn cứ để thêm tiền tố `T` cho mọi bảng. Cách hợp lý là bám theo **nhóm bảng cùng nghiệp vụ** và giữ chính xác tên bảng cũ đang dùng.

| Hiện tại | Điểm chưa thống nhất | Hướng tên đề xuất để xem xét |
|---|---|---|
| `shipments.OrderDrafts` | Khác schema `dbo`; tên bảng tiếng Anh, số nhiều; toàn bộ tên cột tiếng Anh | `dbo.MaVanDon_Nhap`; cần ánh xạ đủ 21 cột và xử lý các cột audit kế thừa, không chỉ đổi tên bảng |
| `dbo.VanDonNguoiTao` | Bảng phụ vận đơn nhưng chưa theo nhóm `MaVanDon_*` | `dbo.MaVanDon_NguoiTao` |
| `dbo.MyTrackingCauHinh` | Bảng cấu hình của khách nhưng tách khỏi nhóm `TCustomer_MyTracking*` | `dbo.TCustomer_MyTracking_CauHinh`, nếu tiếp tục lưu cấu hình portal riêng |
| `dbo.MatHangKhachHang` | Bảng đánh dấu danh mục theo khách, chưa theo nhóm `TCustomer_*` | `dbo.TCustomer_MatHang` hoặc giữ tên hiện tại sau khi thống nhất quy tắc; đây là bảng đánh dấu danh mục, không phải toàn bộ danh sách hàng |
| `dbo.YeuCauSuaNguoiGui` | Bảng yêu cầu theo khách nhưng chưa thể hiện nhóm `TCustomer_*` | `dbo.TCustomer_YeuCauSuaNguoiGui` |
| `dbo.ThongKeSuDung` | Tên bảng tiếng Việt nhưng cột `Id`, `Visitor`, `Audience`, `Path`, `Kind`, `Mode`, `FlowId`, `CreatedAt` bằng tiếng Anh | Nếu chuẩn hóa tiếng Việt: `ID`, `Ma_Khach_Truy_Cap`, `Doi_Tuong`, `Duong_Dan`, `Loai`, `Che_Do`, `Ma_Luong`, `Ngay_Tao` |
| ID tài khoản con ở nhiều bảng | Cùng tham chiếu `TCustomer_NguoiLienLac.ID` nhưng có 4 tên: `StaffID`, `TaiKhoanCon_ID`, `Nhan_Vien_Tao`, `CreatedByStaffId` | Chọn một tên tham chiếu mới, ví dụ `NguoiLienLacID`, khi chuẩn hóa các bảng portal; giữ tên cột cũ nếu hệ thống cũ còn phụ thuộc |
| ID vận đơn ở bảng phụ | `VanDonNguoiTao.MaVanDon_ID` khác tên `MaVanDonID` trong hai bảng chi tiết cũ | Ưu tiên `MaVanDonID` cho bảng phụ portal sau khi chuẩn hóa |
| Ngày giờ | Bảng portal có cả `CreateDate` / `ModifyDate`, `Ngay_Tao` / `Ngay_Cap_Nhat`, `CreatedAt` / `UpdatedAt` | Chọn quy tắc theo nhóm bảng; không đổi kiểu hoặc múi giờ chỉ vì đổi tên |

Đây là đề xuất tên, chưa phải migration đã thực hiện. Các bảng mới độc lập như `NhomHangHoa`, `KenhTMDT`, `KetNoiTMDT`, `DonTMDT`, `ThongBao`, `ThongBao_DaDoc`, `SuCo`, `PhienThuHoi` không có đủ căn cứ để coi tên hiện tại là sai chỉ vì không mang tiền tố `T`.

### MyTracking: cùng nghiệp vụ nhưng không cùng cấu trúc

- Bảng cũ `TCustomer_MyTracking`: 45 cột, khóa `ID` kiểu `int`, các cột nội dung riêng; `TCustomer_MyTracking_Banner` có 16 cột.
- Bảng portal `MyTrackingCauHinh`: 6 cột, khóa `CustomerID` kiểu `bigint`, lưu JSON trong `Cau_Hinh` cùng slug `Duong_Dan` và trạng thái xuất bản.
- Code portal hiện dùng `MyTrackingCauHinh`; dữ liệu không tự đồng bộ với hai bảng MyTracking cũ.
- Đổi tên bảng cấu hình portal sẽ không tự hợp nhất dữ liệu. Nếu mục tiêu là dùng chung trang MyTracking với phần mềm cũ, cần thiết kế chuyển đổi các trường và quy tắc đọc / ghi riêng.

### Tài khoản con: đã dùng bảng cũ

Code hiện ánh xạ tài khoản con vào `TCustomer_NguoiLienLac`, với `TinhCach` = tên đăng nhập, `Mo_Ta` = mật khẩu, `Trang_Thai` = trạng thái. Cột `Quyen` do portal bổ sung. Migration `MoveStaffToNguoiLienLac` đã có trong lịch sử database; bảng tạm `TaiKhoanNhanVien` không còn trong database thực tế.

`TaiKhoanNhanVien`, `GopY`, `GopY_HinhAnh`, `SuCo_TraoDoi` vẫn xuất hiện trong đoạn lịch sử của script migration tổng hợp. Việc xuất hiện trong lịch sử không có nghĩa các bảng này còn là bảng hiện hành; cần đọc toàn bộ script, gồm các bước chuyển và xóa bảng về sau.

## 2. Tám khai báo cột chưa khớp database

| Bảng / cột | Code EF hiện tại | Database thực tế | Ý nghĩa |
|---|---|---|---|
| `TCustomer_NguoiLienLac.Mo_Ta` | Không cho NULL | Cho NULL | Người liên hệ cũ có thể thiếu mật khẩu |
| `TCustomer_NguoiLienLac.Quyen` | Không cho NULL | Cho NULL | Người liên hệ cũ có thể chưa được cấp quyền portal |
| `TCustomer_NguoiLienLac.Ten` | Không cho NULL | Cho NULL | Cần xử lý tên trống khi đọc dữ liệu cũ |
| `TCustomer_NguoiLienLac.TinhCach` | Không cho NULL | Cho NULL | Bảng cũ còn chứa người liên hệ không có tài khoản; query filter hiện đã loại dòng thiếu tên đăng nhập |
| `TCustomer.CustomerCode` | `nvarchar(max)` | `nvarchar(150)` | Cấu hình EF thiếu giới hạn độ dài |
| `TCustomer.Email` | `nvarchar(max)` | `nvarchar(1500)` | Cấu hình EF thiếu giới hạn độ dài |
| `TCustomer.Phone` | `nvarchar(max)` | `nvarchar(150)` | Cấu hình EF thiếu giới hạn độ dài |
| `TCustomer.Tel` | `nvarchar(max)` | `nvarchar(150)` | Cấu hình EF thiếu giới hạn độ dài |

Tại thời điểm kiểm tra, hai dòng người liên hệ có tên đăng nhập đều không NULL ở mật khẩu, tên và quyền. Bốn khác biệt NULL vẫn là rủi ro khi phần mềm cũ thêm hoặc sửa dữ liệu. Cách sửa cần đồng thời xử lý giá trị NULL trong domain / handler; chỉ sửa khai báo EF chưa đủ.

Bốn giới hạn độ dài có thể bổ sung trong cấu hình `LegacyCustomer` để khớp bảng cũ; không cần đổi cấu trúc SQL.

## 3. Liên kết và kiểu khóa

- `MaVanDon.ID` thực tế là `bigint`; `MaVanDon_ChiTietHang.MaVanDonID` và `MaVanDon_PCS_DIM.MaVanDonID` là `int`. Đây là khác biệt đã có trong schema cũ, không phải tên khác nhau nhưng cùng kiểu.
- `VanDonNguoiTao.MaVanDon_ID` dùng `bigint`, khớp kiểu khóa vận đơn.
- Code hiện ép ID vận đơn sang `int` có kiểm tra tràn khi ghi chi tiết (`LegacyOrderLinesWriter`). Nếu ID vượt `2.147.483.647`, việc ghi chi tiết sẽ thất bại. Đổi kiểu cột cũ cần kiểm tra phần mềm cũ và dữ liệu trước.
- Database có 4 ràng buộc khóa ngoại vật lý: `DonTMDT → KetNoiTMDT`, `KetNoiTMDT → KenhTMDT`, `SuCo → SuCo`, `ThongBao_DaDoc → ThongBao`. Nhiều liên kết khách / tài khoản con / quốc gia hiện được ứng dụng kiểm soát, chưa có FK SQL.
- `ThongBao_DaDoc.TaiKhoanCon_ID = 0` có nghĩa tài khoản chính, không phải ID người liên hệ bị mất. Bảy dòng giá trị 0 đã được loại khỏi kiểm tra bản ghi mồ côi theo đúng quy ước này.
- Kết quả 25 liên kết hiện không có bản ghi mồ côi không chứng minh mọi đường ghi dữ liệu tương lai đều được SQL bảo vệ.

## 4. Danh mục Countries có mã trùng

Database có một nhóm `CountryCode` trùng sau khi bỏ khoảng trắng và không phân biệt hoa thường. Script `Countries.txt` cũng có nhóm này:

| CountryID | CountryCode | CountryName trong script |
|---|---|---|
| 90 | VA | Holy See (Vatican City) |
| 288 | VA | Vatican City |

Lookup vừa bổ sung trả NULL khi một mã khớp nhiều ID, nên mã `VA` cần quy tắc xác định ID chuẩn trước khi bảo đảm điền được `ConsigneeCountryID` cho mọi nước. Không nên tự xóa hoặc gộp ID khi chưa đối chiếu những vận đơn cũ đang tham chiếu.

## 5. Danh sách 14 bảng portal hiện hành

| Bảng | Số cột | Nhận xét về tên |
|---|---:|---|
| `shipments.OrderDrafts` | 21 | Khác rõ nhất so với các bảng nghiệp vụ `dbo` |
| `dbo.VanDonNguoiTao` | 4 | Nên xem xét đưa về nhóm `MaVanDon_*` |
| `dbo.NhomHangHoa` | 7 | Danh mục độc lập; cột chủ yếu tiếng Việt và ngày giờ theo kiểu cũ |
| `dbo.MatHangKhachHang` | 8 | Cân nhắc nhóm `TCustomer_*` |
| `dbo.KenhTMDT` | 10 | Cùng nhóm nghiệp vụ TMDT |
| `dbo.KetNoiTMDT` | 23 | Cùng nhóm nghiệp vụ TMDT |
| `dbo.DonTMDT` | 37 | Cùng nhóm nghiệp vụ TMDT; tên ID tài khoản con khác các bảng khác |
| `dbo.MyTrackingCauHinh` | 6 | Cấu hình portal riêng, chưa chung tên / dữ liệu với MyTracking cũ |
| `dbo.ThongBao` | 12 | Tên bảng tiếng Việt; cột nội dung tiếng Việt |
| `dbo.ThongBao_DaDoc` | 5 | Có tiền tố bảng cha; tên ID tài khoản con cần thống nhất |
| `dbo.SuCo` | 26 | Tên bảng tiếng Việt; tên `StaffID` khác nhóm tài khoản con khác |
| `dbo.PhienThuHoi` | 3 | Bảng nghiệp vụ phiên đăng nhập độc lập |
| `dbo.ThongKeSuDung` | 8 | Tên bảng tiếng Việt nhưng cả 8 cột tiếng Anh |
| `dbo.YeuCauSuaNguoiGui` | 22 | Cân nhắc nhóm `TCustomer_*` |

## 6. Thứ tự xử lý đề xuất

1. Khớp khai báo NULL / độ dài với bảng thực tế và xử lý dữ liệu cũ thiếu trường.
2. Chốt quy tắc đặt tên theo nhóm: `TCustomer_*`, `MaVanDon_*`, danh mục và chức năng độc lập.
3. Lập migration đổi tên có bảo toàn dữ liệu, cập nhật EF mapping, tên cột trong index / filter / SQL trực tiếp, script triển khai và tài liệu cùng lúc. Với bảng dùng bởi hệ thống cũ, kiểm tra thêm các ứng dụng và đối tượng SQL phụ thuộc.
4. Xác định ID quốc gia chuẩn cho mã trùng và đánh giá khả năng mở rộng khóa liên kết `int` của chi tiết vận đơn.

Việc đổi tên cột C# không bắt buộc giống SQL: có thể giữ tên property để giảm ảnh hưởng tới API và ánh xạ tên SQL đúng bằng `HasColumnName` / `ToTable`.
