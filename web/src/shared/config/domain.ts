/** Hằng số nghiệp vụ dùng chung giữa nhiều feature. */

import { CARRIER_EXCLUDED, HUB_COUNTRIES } from './hub-countries';

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

/**
 * Danh sách DỰ PHÒNG hãng → hub (chép từ hệ thống cũ) — chỉ dùng khi chưa đọc được danh mục thật
 * (GET /services/hubs đọc dbo.TGroupBaseCode / dbo.TBaseCode, 06/11/2026; vd laptop chưa có 2 bảng, mạng lỗi).
 */
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
 * Danh mục hãng / dịch vụ → hub. Giá trị hub dạng "Hãng - Hub" (vd "DHL - Singapore"); backend ghi MaVanDon.Dich_Vu = "DHL|Singapore"
 * — tên phải trùng đúng chữ danh mục cũ (proc in bill / CMS khớp theo chuỗi). Thứ tự khóa = thứ tự hãng.
 */
export type ServiceCatalog = Readonly<Record<string, readonly string[]>>;

/** Danh mục dự phòng (xem HUB_NAMES). */
export const CARRIER_HUBS: ServiceCatalog = Object.fromEntries(
  Object.entries(HUB_NAMES).map(([carrier, hubs]) => [carrier, hubs.map(h => `${carrier} - ${h}`)])
);
export const CARRIERS = Object.keys(CARRIER_HUBS);

/** Dữ liệu GET /services/hubs → danh mục; rỗng (backend chưa đọc được bảng) → danh mục dự phòng. Hàm thuần — có test. */
export function toServiceCatalog(rows: ReadonlyArray<{ carrier: string; hubs: ReadonlyArray<{ name: string }> }> | undefined): ServiceCatalog {
  if (!rows?.length) return CARRIER_HUBS;
  return Object.fromEntries(rows.map(r => [r.carrier, r.hubs.map(h => `${r.carrier} - ${h.name}`)]));
}

/** "Chuyên tuyến - USA - Sea" → "USA - Sea" (tên hub hiển thị trong ô chọn). Hãng tên dài khớp trước. */
export function hubLabel(hub: string, catalog: ServiceCatalog = CARRIER_HUBS): string {
  const carrier = Object.keys(catalog).sort((a, b) => b.length - a.length).find(c => hub.startsWith(`${c} - `));
  return carrier ? hub.slice(carrier.length + 3) : hub;
}

/**
 * Hub của hãng đi được tới nước đến (mã ISO-2) theo bảng CS điền (HUB_COUNTRIES) và danh sách nước hãng không / ngưng phục vụ
 * (CARRIER_EXCLUDED). Chưa biết mã nước → mọi hub của hãng; hub chưa có trong bảng → coi như đi mọi nước. Hàm thuần — có test.
 */
export function hubsFor(carrier: string, countryCode?: string, catalog: ServiceCatalog = CARRIER_HUBS): readonly string[] {
  const hubs = catalog[carrier] ?? [];
  const code = countryCode?.trim().toUpperCase();
  if (!code || code.length !== 2) return hubs;
  if (CARRIER_EXCLUDED[carrier]?.includes(code)) return [];
  return hubs.filter(hub => {
    const countries = HUB_COUNTRIES[`${carrier}|${hubLabel(hub, catalog)}`] ?? '*';
    return countries === '*' || countries.includes(code);
  });
}

/** Hãng có ít nhất 1 hub đi được tới nước đến (ẩn hãng không phục vụ nước đó). Hàm thuần — có test. */
export const carriersFor = (countryCode?: string, catalog: ServiceCatalog = CARRIER_HUBS): string[] =>
  Object.keys(catalog).filter(c => hubsFor(c, countryCode, catalog).length > 0);

/** Lựa chọn cho ô Hub (lọc theo nước đến nếu có): giá trị đầy đủ, nhãn chỉ tên hub. */
export const hubOptions = (carrier: string, countryCode?: string, catalog: ServiceCatalog = CARRIER_HUBS) =>
  hubsFor(carrier, countryCode, catalog).map(value => ({ value, label: hubLabel(value, catalog) }));

/** Hub mặc định khi đổi hãng / nước: chỉ còn 1 hub thì chọn luôn, nhiều hub thì để khách chọn. */
export function defaultHub(carrier: string, countryCode?: string, catalog: ServiceCatalog = CARRIER_HUBS): string {
  const hubs = hubsFor(carrier, countryCode, catalog);
  return hubs.length === 1 ? hubs[0]! : '';
}

/** Nước đến bắt buộc khai Tỉnh / Bang: Mỹ, Canada, Úc (cùng danh sách backend ReceiverStateRule). */
export const STATE_REQUIRED_COUNTRIES: readonly string[] = ['US', 'CA', 'AU'];

const STATE_REQUIRED_NAMES = new Set(['united states', 'united states of america', 'usa', 'us', 'canada', 'australia']);

/** Có bắt buộc Tỉnh / Bang không — theo mã nước ISO-2 (form tự điền), thiếu mã thì theo tên nước tiếng Anh. Hàm thuần — có test. */
export function stateRequired(country: string | undefined, countryCode?: string): boolean {
  const code = countryCode?.trim().toUpperCase();
  if (code && code.length === 2) return STATE_REQUIRED_COUNTRIES.includes(code);
  return STATE_REQUIRED_NAMES.has((country ?? '').trim().toLowerCase());
}

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
