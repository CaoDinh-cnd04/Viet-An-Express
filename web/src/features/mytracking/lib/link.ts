/**
 * Link trang MyTracking gửi người nhận hàng: {tên miền MyTracking}/tracking/{mã khách}/{số bill Việt An}?ref={mã REF}.
 * VD https://mytracking.top/tracking/SPVN/6174086?ref=PO-1.
 */

/**
 * Tên miền trang MyTracking (đặt lúc build bằng VITE_MYTRACKING_URL); bỏ "/" cuối.
 * Chạy trên máy (npm run dev) mặc định là chính portal đang chạy — mytracking.top hiện vẫn là hệ thống cũ.
 */
export const MYTRACKING_URL: string = (
  import.meta.env.VITE_MYTRACKING_URL ?? (import.meta.env.DEV && typeof window !== 'undefined' ? window.location.origin : 'https://mytracking.top')
).replace(/\/+$/, '');

/** Link đầy đủ; thiếu số bill → link gốc của khách (người nhận tự nhập số bill). Hàm thuần — có test. */
export function customerTrackingUrl(base: string, customerCode: string, bill = '', ref = ''): string {
  const path = `${base}/tracking/${encodeURIComponent(customerCode.trim())}/${bill.trim() ? encodeURIComponent(bill.trim()) : ''}`;
  return ref.trim() ? `${path}?ref=${encodeURIComponent(ref.trim())}` : path;
}

/** Trang đang mở trên tên miền MyTracking (vd mytracking.top) — ở đó /tracking/{mã khách} là trang của khách. Hàm thuần — có test. */
export function isMyTrackingHost(hostname: string, base: string = MYTRACKING_URL): boolean {
  try {
    return new URL(base).hostname.toLowerCase() === hostname.toLowerCase();
  } catch {
    return false;
  }
}

/**
 * Mã cần tra trên trang của khách: số bill trên đường dẫn (nhiều mã ngăn dấu phẩy); không có thì mã REF.
 * Viết hoa như backend trả về (để khớp kết quả). Hàm thuần — có test.
 */
export function customerTrackingQuery(billPath: string | undefined, ref: string | null): string[] {
  const bills = [...new Set((billPath ?? '').split(',').map(b => b.trim().toUpperCase()).filter(Boolean))];
  if (bills.length > 0) return bills;
  const r = ref?.trim().toUpperCase();
  return r ? [r] : [];
}

/**
 * Nơi chứa ảnh hệ thống MyTracking cũ đã tải lên (Logo, ảnh nền, ảnh quảng cáo lưu dạng "/Upload/Banner/…").
 * Đặt lúc build bằng VITE_MYTRACKING_ASSET_URL — khi mytracking.top chuyển sang portal mới phải chép thư mục Upload sang
 * hoặc trỏ biến này về nơi còn giữ ảnh.
 */
export const MYTRACKING_ASSET_URL: string = (import.meta.env.VITE_MYTRACKING_ASSET_URL ?? 'https://mytracking.top').replace(/\/+$/, '');

/** Đường dẫn tương đối của hệ thống cũ ("/Upload/…") → link đầy đủ; link http(s) / data URL giữ nguyên. Hàm thuần — có test. */
export function legacyAssetUrl(src: string, base: string = MYTRACKING_ASSET_URL): string {
  const value = src.trim();
  return /^\/(?!\/)/.test(value) ? `${base}${value}` : value;
}

