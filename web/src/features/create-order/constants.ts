/** Hằng số nghiệp vụ tạo đơn. Thêm hãng / hub / đơn vị mới: sửa ở đây, UI tự cập nhật. */

export { CARRIER_HUBS, CARRIERS, DEFAULT_SERVICE, defaultHub, hubOptions } from '@/shared/config/domain';

export { COUNTRIES } from '@/shared/config/domain';

/** Giá trị "nhóm hàng" đặc biệt: kiện gồm nhiều nhóm → khai bảng phân loại. */
export const MULTI_CATEGORY = 'Nhiều loại hàng';

export const PACKAGING_TYPES = ['Thùng carton', 'Bao / túi', 'Pallet', 'Kiện gỗ'] as const;
/** Quy cách (ĐVT) mặt hàng: danh sách có sẵn + "Khác" (khách tự nhập) — xem lib/units.ts. */
export { OTHER_UNIT, PRESET_UNITS } from './lib/units';
/** Đơn vị tiền tệ của invoice — lưu mã vào MaVanDon.Loai_Tien. */
/** Hình thức chịu thuế nhập khẩu tại nước đến (Incoterm). Mặc định DDU. */
export const DUTY_TERMS = [
  { value: 'DDU', label: 'DDU — người nhận chịu thuế' },
  { value: 'DDP', label: 'DDP — người gửi chịu thuế' }
] as const;

export const CURRENCIES = ['USD', 'SGD', 'EUR', 'GBP', 'AUD'] as const;
/** Hình thức xuất khẩu — lưu mã vào MaVanDon.Ly_Do_Xuat_Hang (dữ liệu cũ: GIFT, SAMPLE). */
export const EXPORT_TYPES = [
  { value: 'GIFT', label: 'gift (no commercial value)' },
  { value: 'SAMPLE', label: 'sample' },
  { value: 'OTHER', label: 'khác' }
] as const;

/**
 * Tùy chọn dịch vụ (Service options) — song ngữ Việt / Anh.
 * `name` là giá trị lưu vào đơn (giữ nguyên để đơn nháp cũ vẫn khớp).
 */
export const ADDONS = [
  {
    name: 'Phát có chữ ký người nhận',
    nameEn: 'Signature required',
    description: 'Người nhận ký xác nhận khi giao hàng',
    descriptionEn: 'Receiver signs to confirm delivery'
  },
  {
    name: 'Đóng gói hộ',
    nameEn: 'Packing service',
    description: 'Việt An đóng gói / gia cố kiện hàng',
    descriptionEn: 'Viet An packs or reinforces your parcels'
  },
  {
    name: 'Gửi email thông báo',
    nameEn: 'Email notification',
    description: 'Tự động gửi email trạng thái đơn cho người nhận',
    descriptionEn: 'Automatic shipment status emails to the receiver'
  },
  {
    name: 'Đóng thuế hộ (DDP)',
    nameEn: 'Duties & taxes paid (DDP)',
    description: 'Việt An ứng và đóng thuế nhập khẩu tại nước đến',
    descriptionEn: 'Viet An pays import duties and taxes at destination'
  },
  {
    name: 'Khai giá / Bảo hiểm hàng',
    nameEn: 'Declared value / Insurance',
    description: 'Khai giá trị hàng để được bảo hiểm rủi ro',
    descriptionEn: 'Declare the goods value to insure against loss or damage'
  },
  {
    name: 'Dán nhãn Fragile (dễ vỡ)',
    nameEn: 'Fragile label',
    description: 'Xử lý nhẹ tay, dán nhãn cảnh báo dễ vỡ',
    descriptionEn: 'Handle with care, fragile warning label applied'
  }
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

export const WIZARD_STEPS = [
  { title: 'Thông tin bill', description: 'Người gửi, người nhận, dịch vụ' },
  { title: 'Kiện & nội dung hàng', description: 'Kích thước, nhóm hàng, tùy chọn dịch vụ' },
  { title: 'Invoice & phí', description: 'Khai hàng, shipping fee' }
] as const;

/** Chứng từ (DOC): không khai kiện / Invoice → chỉ 2 bước. */
export const WIZARD_STEPS_DOC = [
  WIZARD_STEPS[0],
  { title: 'Tùy chọn dịch vụ', description: 'Dịch vụ cộng thêm (nếu cần)' }
] as const;

export type WizardStep = { readonly title: string; readonly description: string };

/** Liên kết tra cứu (khung "Hỗ trợ" của Bill Online cũ). `url` để trống = chưa có trang. */
export const HELP_LINKS: ReadonlyArray<{ group: string; links: ReadonlyArray<{ label: string; url?: string }> }> = [
  { group: 'Hướng dẫn', links: [{ label: 'Video hướng dẫn tạo bill' }, { label: 'Tra cứu mã HS', url: 'https://vietanexpress.com/tra-hs-code' }] },
  // Trang chính thức của hãng tại Việt Nam (kiểm tra 30/09/2026).
  {
    group: 'Phụ phí xăng dầu',
    links: [
      { label: 'DHL', url: 'https://mydhl.express.dhl/vn/vi/important-information/weekly-fuel-surcharge.html' },
      { label: 'Fedex', url: 'https://www.fedex.com/vi-vn/shipping/surcharges.html' },
      { label: 'UPS', url: 'https://www.ups.com/vn/vi/support/shipping-support/shipping-costs-rates/fuel-surcharges' }
    ]
  },
  {
    group: 'Phụ phí cộng thêm của hãng bay',
    links: [
      { label: 'UPS', url: 'https://www.ups.com/vn/vi/support/shipping-support/shipping-costs-rates' },
      { label: 'DHL', url: 'https://mydhl.express.dhl/vn/vi/ship/surcharges.html' },
      { label: 'Fedex', url: 'https://www.fedex.com/vi-vn/shipping/surcharges/other-surcharges.html' },
      { label: 'Điều kiện vận chuyển Fedex', url: 'https://www.fedex.com/vi-vn/conditions-of-carriage.html' }
    ]
  },
  {
    group: 'Kiểm tra vùng sâu vùng xa (VSVX)',
    links: [
      // DHL, UPS: file Excel danh sách vùng sâu vùng xa do hãng công bố.
      { label: 'DHL', url: 'https://mydhl.express.dhl/content/dam/downloads/global/en/remote-areas/dhl_express_remote_areas_en.xlsx.coredownload.xlsx' },
      { label: 'Fedex', url: 'https://www.fedex.com/vi-vn/customer-support/faq/invoices-and-payments/fuel-and-other-surcharges/out-of-delivery-pick-up-area-surcharge.html' },
      { label: 'UPS', url: 'https://assets.ups.com/adobe/assets/urn:aaid:aem:76be2504-c73d-4d94-b4db-550997a8f095/original/as/ea-surcharge-vn-vi.xlsx' }
    ]
  }
];
