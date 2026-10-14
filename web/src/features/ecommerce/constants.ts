import type { Tone } from '@/shared/ui';
import type { EcomSettings, EcomSource, EcomStatus, StoreConnectionStatus, StorePlatform, TiktokRegion } from './types';

export const ECOM_SOURCES: Record<EcomSource, { label: string }> = {
  tiktok: { label: 'TikTok Shop' },
  // Đã bỏ tích hợp Shopify — giữ nhãn để hiện đơn cũ có nguồn shopify trong dbo.DonTMDT.
  shopify: { label: 'Shopify' },
  shopee: { label: 'Shopee' },
  lazada: { label: 'Lazada' },
  amazon: { label: 'Amazon' },
  ebay: { label: 'eBay' },
  etsy: { label: 'Etsy' },
  woocommerce: { label: 'WooCommerce' },
  api: { label: 'API' },
  excel: { label: 'Excel' },
  manual: { label: 'Nhập tay' }
};

export const ECOM_STATUS: Record<EcomStatus, { label: string; tone: Tone }> = {
  created: { label: 'Đã tạo', tone: 'success' },
  picked_up: { label: 'Đã lấy', tone: 'info' },
  departed: { label: 'Đã đi', tone: 'success' },
  delivered: { label: 'Đã phát', tone: 'brand' },
  exception: { label: 'Lỗi', tone: 'danger' },
  weighing: { label: 'Chờ cân đo', tone: 'warning' }
};

export const STORE_PLATFORMS: Record<StorePlatform, { label: string }> = {
  // Shop Shopify kết nối trước khi bỏ tích hợp vẫn có thể còn trong dbo.KetNoiTMDT (chỉ ngắt được).
  shopify: { label: 'Shopify' },
  tiktok: { label: 'TikTok Shop' }
};

export const TIKTOK_REGIONS: ReadonlyArray<{ value: TiktokRegion; label: string }> = [
  { value: 'global', label: 'Toàn cầu' },
  { value: 'us', label: 'Hoa Kỳ (US)' }
];

export const STORE_STATUS: Record<StoreConnectionStatus, { label: string; tone: Tone }> = {
  active: { label: 'Đang hoạt động', tone: 'success' },
  expired: { label: 'Hết hạn ủy quyền', tone: 'warning' },
  error: { label: 'Lỗi đồng bộ', tone: 'danger' },
  revoked: { label: 'Shop đã gỡ ứng dụng', tone: 'neutral' }
};

export const WEBHOOK_EVENTS: ReadonlyArray<EcomSettings['webhookEvents'][number]> = ['created', 'picked_up', 'departed', 'delivered', 'exception'];

/** File mẫu CSV 70 cột nhập đơn E-commerce. */
export const ECOM_IMPORT_TEMPLATE_URL = '/templates/VietAn_Ecom_Import_Template.csv';

export const MAX_PRODUCTS = 5;

/** Tên file mẫu import — cấu trúc 70 cột của Yun/EPK/Việt An. */

/** Ví dụ body tạo đơn — giá trị gửi API giữ nguyên tiếng Việt (tên dịch vụ, hub). */
const API_EXAMPLE_BODY = {
  shopOrderRef: 'TT-88213',
  source: 'tiktok',
  service: 'Chuyên tuyến',
  hub: 'Chuyên tuyến - Singapore',
  branch: 'TP.HCM',
  receiver: { name: 'Emma W.', country: 'Singapore', city: 'Singapore', postal: '238859', phone: '+65 8123 4567', address1: '1 Raffles Place' },
  parcel: { weightKg: 1.2, lengthCm: 25, widthCm: 18, heightCm: 10 },
  items: [{ sku: 'DR-001', nameEn: 'Dress', nameVi: 'Váy', hs: '6204.43', origin: 'VN', qty: 2, unitPrice: 8, currency: 'USD' }]
};

export const API_EXAMPLE = [
  'curl -X POST https://api.vietanexpress.com.vn/v1/orders \\',
  '  -H "Authorization: Bearer <API_KEY>" \\',
  '  -H "Content-Type: application/json" \\',
  `  -d '${JSON.stringify(API_EXAMPLE_BODY, null, 2)}'`
].join('\n');
