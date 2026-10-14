/** Dữ liệu E-commerce — hợp đồng API: docs/API_CONTRACT.md §5. */
export type EcomSource = 'tiktok' | 'shopify' | 'shopee' | 'lazada' | 'amazon' | 'ebay' | 'etsy' | 'woocommerce' | 'api' | 'excel' | 'manual';
export type EcomStatus = 'created' | 'picked_up' | 'departed' | 'delivered' | 'exception' | 'weighing';

export interface EcomProduct {
  name: string;
  sku: string;
  qty: number;
  fobPrice: number;
  sellingPrice: number;
  weightKg?: number;
  hsCode?: string;
  origin?: string;
}

export interface EcomOrder {
  id: string;
  src: EcomSource;
  /** Mã đơn của shop / sàn */
  ref: string;
  /** Mã bill Việt An — rỗng khi lỗi / chờ xử lý */
  bill: string;
  cnee: string;
  ct: string;
  items: number;
  kg: number;
  st: EcomStatus;
  note?: string;
  products?: EcomProduct[];
  /** "dd/MM/yyyy HH:mm" — ngày đặt trên sàn / ngày nhập */
  createdAt: string;
  /** Tổng giá trị đơn theo tiền tệ của shop */
  value?: number | null;
  currency?: string | null;
  receiver?: EcomReceiver;
  /** Đơn nhập tay: hãng / dịch vụ, hub, chi nhánh gửi đã chọn */
  service?: string | null;
  hub?: string | null;
  branch?: string | null;
  /** Việc cần bổ sung trước khi tạo bill (backend tính: thiếu cân nặng, địa chỉ chưa Latin, thiếu mã HS…) */
  issues?: string[];
  /** Chưa có bill → sửa được */
  editable?: boolean;
  /** Xóa được: đơn nhập tay / file chưa gửi. Đơn đồng bộ từ shop đã kết nối không xóa trong portal (hủy / hoàn tất trên sàn). */
  deletable?: boolean;
  /** Đã xác nhận gửi → nằm ở trang "Đơn hàng E-com" */
  confirmed?: boolean;
  /** "dd/MM/yyyy HH:mm" */
  confirmedAt?: string | null;
}

/** PUT /ecom/orders/:id — khách sửa đơn trước khi tạo bill. */
export interface EcomOrderUpdate {
  receiver: { name: string; company: string; phone: string; email: string; address1: string; address2: string; city: string; state: string; postal: string; countryCode: string };
  kg: number | null;
  products: Array<{ name: string; sku: string; qty: number; fobPrice: number; sellingPrice: number; hsCode: string }>;
  service?: string | null;
  hub?: string | null;
  branch?: string | null;
  note: string;
}

export interface EcomReceiver {
  name?: string | null;
  company?: string | null;
  phone?: string | null;
  email?: string | null;
  address1?: string | null;
  address2?: string | null;
  city?: string | null;
  state?: string | null;
  postal?: string | null;
  countryCode?: string | null;
  country?: string | null;
}

export interface NewManualEcomOrder {
  ref: string;
  source: EcomSource;
  cnee: string;
  phone: string;
  email?: string;
  /** Tên nước tiếng Anh, như /geo/countries */
  ct: string;
  /** Mã ISO 2 ký tự — trống khi danh sách nước từ API lỗi */
  countryCode?: string;
  postal: string;
  city: string;
  state: string;
  address: string;
  /** Để trống nếu Việt An cân */
  kg: number;
  products: Array<{ name: string; sku: string; qty: number; fobPrice: number; sellingPrice: number }>;
}


export interface CsvImportResult {
  message: string;
  importedCount: number;
  errors: Array<{ row: number; message: string } | string>;
}

/** Cấu hình kết nối — endpoint mới, backend SQL Server cần làm (xem API_CONTRACT.md). */
export interface EcomSettings {
  apiKeys: Array<{ env: 'production' | 'sandbox'; key: string }>;
  webhookUrl: string;
  webhookEvents: Array<'created' | 'picked_up' | 'departed' | 'delivered' | 'exception'>;
}

/** Sàn kết nối qua OAuth — backend giữ token, tự nhận đơn mới và trả tracking; frontend chỉ thấy trạng thái. */
export type StorePlatform = 'shopify' | 'tiktok';
/** TikTok Shop tách cổng ủy quyền: global (UK, EU, SEA…) và US. */
export type TiktokRegion = 'global' | 'us';
export type StoreConnectionStatus = 'active' | 'expired' | 'error' | 'revoked';

export interface StoreConnection {
  id: string;
  platform: StorePlatform;
  shopName: string;
  /** TikTok: mã shop / shop_cipher rút gọn */
  shopDomain?: string;
  region?: string;
  status: StoreConnectionStatus;
  connectedAt: string;
  lastSyncAt?: string;
  lastError?: string;
}

export interface StartStoreConnection {
  platform: StorePlatform;
  shopDomain?: string;
  region?: TiktokRegion;
}

export interface StoreSyncResult {
  message: string;
  importedCount: number;
}
