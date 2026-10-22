/** Hằng số nghiệp vụ dùng chung giữa nhiều feature. */

export const BRANCHES = ['TP.HCM', 'Hà Nội', 'Huế', 'Bảo Lộc', 'Cần Thơ'] as const;
export type Branch = (typeof BRANCHES)[number];

export type CargoType = 'DOC' | 'PACK';

export const TRACKING_URL = 'https://vietanexpress.com.vn/track';
/** Trang tra cứu vận đơn của Việt An (nút VA Track). */
export const VA_TRACKING_URL = 'https://vietanexpress.com.vn/TrackingResult.aspx';
export const vaTrackingLink = (bill: string) => `${VA_TRACKING_URL}?id=${encodeURIComponent(bill)}`;
export const trackingLink = (bill: string, agentBrand = false) =>
  `${TRACKING_URL}?id=${encodeURIComponent(bill)}${agentBrand ? '&brand=1' : ''}`;

/** Danh sách nước đến gợi ý (datalist). Backend sẽ trả danh mục đầy đủ qua GET /addresses/countries. */
export const COUNTRIES = [
  'Singapore', 'Malaysia', 'Thailand', 'Taiwan', 'China', 'Hong Kong', 'Japan', 'South Korea', 'Australia', 'New Zealand',
  'United States', 'Canada', 'Mexico', 'United Kingdom', 'Germany', 'France', 'Belgium', 'Netherlands', 'Italy', 'Spain',
  'United Arab Emirates', 'Saudi Arabia', 'India', 'Philippines', 'Indonesia', 'Cambodia', 'Laos', 'Bolivia'
] as const;

/** Tên hub theo hãng — đúng danh sách của hệ thống cũ. */
const HUB_NAMES: Record<string, readonly string[]> = {
  Aramex: ['Dubai'],
  DHL: ['VN', 'SIN VIP', 'Singapore', 'UK', 'Dubai'],
  Fedex: ['SIN 1', 'SIN 2', 'SIN 3 (FICP)', 'Singapore', 'UK', 'VN'],
  UPS: ['Dubai', 'Singapore', 'UK', 'VN Saver', 'TaiWan', 'VN Expedited'],
  'Chuyên tuyến': [
    'Sin (Đông Lạnh)', 'Singapore', 'UK', 'AU_AuPost', 'AU_Toll (T.Phẩm/M.Phẩm)', 'AU_Toll Vip (Hàng thường)', 'Canada', 'Dubai',
    'Indonesia', 'Korea', 'Laos', 'Saudi', 'USA', 'USA - Sea', 'EU', 'Japan', 'Malaysia', 'China', 'India',
    'Russia', 'Taiwan', 'Hong Kong', 'Thailand - AIR', 'Thailand - Truck', 'Cambodia', 'Philippines', 'VA'
  ],
  'Ủy quyền Việt An': ['Tự chọn'],
  Ecommerce: ['SING POST', 'Việt An'],
  SEA: ['Australia', 'Dubai', 'Indonesia', 'Malaysia', 'Philippine', 'Singapore', 'USA']
};

/**
 * Hãng / dịch vụ → hub. Giá trị hub dạng "Hãng - Hub" (vd "DHL - Singapore"); backend ghi MaVanDon.Dich_Vu = "DHL|Singapore".
 * Backend sẽ trả qua GET /services/hubs; tạm khai ở đây.
 */
export const CARRIER_HUBS: Record<string, readonly string[]> = Object.fromEntries(
  Object.entries(HUB_NAMES).map(([carrier, hubs]) => [carrier, hubs.map(h => `${carrier} - ${h}`)])
);
export const CARRIERS = Object.keys(CARRIER_HUBS);

/** "Chuyên tuyến - USA - Sea" → "USA - Sea" (tên hub hiển thị trong ô chọn). */
export function hubLabel(hub: string): string {
  const carrier = CARRIERS.find(c => hub.startsWith(`${c} - `));
  return carrier ? hub.slice(carrier.length + 3) : hub;
}

/** Lựa chọn cho ô Hub: giá trị đầy đủ, nhãn chỉ tên hub. */
export const hubOptions = (carrier: string) => (CARRIER_HUBS[carrier] ?? []).map(value => ({ value, label: hubLabel(value) }));

/** Hub mặc định khi đổi hãng: hãng chỉ có 1 hub thì chọn luôn, nhiều hub thì để khách chọn. */
export function defaultHub(carrier: string): string {
  const hubs = CARRIER_HUBS[carrier] ?? [];
  return hubs.length === 1 ? hubs[0]! : '';
}

/** Dịch vụ mặc định của đơn mới. */
export const DEFAULT_SERVICE = { carrier: 'Chuyên tuyến', hub: 'Chuyên tuyến - Singapore' } as const;

/**
 * Tuyến khai invoice riêng từng kiện (mỗi kiện 1 invoice, số invoice = tổng số kiện).
 * Giá trị hub dạng "Hãng - Hub"; "Hãng - *" = mọi hub của hãng. Thêm tuyến mới: thêm 1 dòng ở đây —
 * backend không cần sửa (đơn có dòng hàng mang số kiện thì in mỗi kiện 1 invoice).
 */
export const PER_PACKAGE_INVOICE_HUBS: readonly string[] = [
  'Chuyên tuyến - AU_AuPost',
  'Chuyên tuyến - AU_Toll (T.Phẩm/M.Phẩm)',
  'Chuyên tuyến - AU_Toll Vip (Hàng thường)',
  'Chuyên tuyến - EU',
  'Chuyên tuyến - Canada'
];

/** Hub có khai invoice riêng từng kiện không (khớp cả dạng "Hãng - *"). */
export function isPerPackageInvoice(hub: string | undefined, rules: readonly string[] = PER_PACKAGE_INVOICE_HUBS): boolean {
  const value = hub?.trim() ?? '';
  if (!value) return false;
  return rules.some(rule => (rule.endsWith(' - *') ? value.startsWith(rule.slice(0, -1)) : rule === value));
}
