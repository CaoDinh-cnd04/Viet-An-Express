/** Hằng số của mô hình form đơn hàng (dùng chung cho tạo đơn, đơn nháp, kiểm tra form). */

/** Giá trị "nhóm hàng" đặc biệt: kiện gồm nhiều nhóm → khai bảng phân loại. */
export const MULTI_CATEGORY = 'Nhiều loại hàng';

/** Hình thức xuất khẩu — lưu mã vào MaVanDon.Ly_Do_Xuat_Hang (dữ liệu cũ: GIFT, SAMPLE). */
export const EXPORT_TYPES = [
  { value: 'GIFT', label: 'gift (no commercial value)' },
  { value: 'SAMPLE', label: 'sample' },
  { value: 'OTHER', label: 'khác' }
] as const;

/** Quy tắc nghiệp vụ (xem CLAUDE.md §4). */
export const RULES = {
  /** Chứng từ nặng hơn mức này tự chuyển sang hàng hóa (DocToPackRule). */
  docMaxWeightKg: 2,
  /** Hệ số quy đổi thể tích: D×R×C / divisor. */
  volumetricDivisor: 5000,
  /** Đơn giá invoice: tối đa 2 chữ số sau dấu chấm (chuẩn khai báo quốc tế). */
  priceDecimals: 2,
  shipperAddressMax: 60,
  /** Mô tả tổng quan hàng hóa (content) — độ dài cột dbo.MaVanDon.Ten_hang. */
  contentMax: 150,
  /** Tên shipper gốc (FWD) — độ dài cột dbo.MaVanDon.Ten_Khach_Cua_FWD. */
  originalShipperMax: 150,
  /** Tên nhóm hàng — độ dài cột dbo.NhomHangHoa.Ten_Nhom. */
  categoryNameMax: 150,
  receiverAddressMax: 30,
  /** Địa chỉ 3: không giới hạn 30 như địa chỉ 1 / 2, chỉ chặn ở độ dài cột ConsigneeAddress3 (nvarchar 250) trong dbo.MaVanDon. */
  receiverAddress3Max: 250
} as const;
