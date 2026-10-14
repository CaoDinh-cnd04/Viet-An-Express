import type { StoreConnection } from '../types';

/** Kết nối cần khách ủy quyền lại (token hết hạn / bị thu hồi trên sàn). */
export const needsReauthorize = (c: Pick<StoreConnection, 'status'>): boolean => c.status === 'expired' || c.status === 'revoked';

/** Đọc kết quả backend gắn vào URL sau khi sàn redirect về: ?tab=connect&connected=tiktok | &error=... */
export function readOAuthResult(params: URLSearchParams): { ok: true; platform: string } | { ok: false; error: string } | null {
  const error = params.get('error');
  if (error) return { ok: false, error };
  const platform = params.get('connected');
  return platform ? { ok: true, platform } : null;
}

export const ECOM_TABS = ['orders', 'add', 'connect'] as const;
export type EcomTab = (typeof ECOM_TABS)[number];

/** Trang "Đơn hàng E-com" (đơn đã xác nhận gửi) — mục riêng trên menu Dịch vụ & Bán hàng. */
export const ECOM_ORDERS_PATH = '/ecommerce/orders';

/** Tab cũ (overview, push, stores, conn) vẫn mở đúng chỗ sau khi gộp còn 3 tab. */
const LEGACY_TABS: Record<string, EcomTab> = { overview: 'orders', push: 'add', stores: 'connect', conn: 'connect' };

export function resolveEcomTab(raw: string | null): EcomTab {
  if (!raw) return 'orders';
  return (ECOM_TABS as readonly string[]).includes(raw) ? (raw as EcomTab) : LEGACY_TABS[raw] ?? 'orders';
}

/** "2026-10-05T14:54:25.6+07:00" → "05/10/2026 14:54" (giờ backend gửi đã là giờ Việt Nam). */
export function formatSyncTime(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(iso);
  return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : iso;
}
