/** Hằng số nghiệp vụ tạo đơn. Thêm hãng / hub / đơn vị mới: sửa ở đây, UI tự cập nhật. */

export { CARRIER_HUBS, CARRIERS, DEFAULT_SERVICE, defaultHub, hubOptions, COUNTRIES } from '@/shared/config/domain';


export const PACKAGING_TYPES = ['Thùng carton', 'Bao / túi', 'Pallet', 'Kiện gỗ'] as const;
/** Quy cách (ĐVT) mặt hàng: danh sách có sẵn + "Khác" (khách tự nhập) — xem lib/units.ts. */
export { OTHER_UNIT, PRESET_UNITS } from '@/features/order-form';
/** Đơn vị tiền tệ của invoice — lưu mã vào MaVanDon.Loai_Tien. */
/** Hình thức chịu thuế nhập khẩu tại nước đến (Incoterm). Mặc định DDU. */
export const DUTY_TERMS = [
  { value: 'DDU', label: 'DDU — người nhận chịu thuế' },
  { value: 'DDP', label: 'DDP — người gửi chịu thuế' }
] as const;

export const CURRENCIES = ['USD', 'SGD', 'EUR', 'GBP', 'AUD'] as const;

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
