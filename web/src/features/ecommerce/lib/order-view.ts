import type { Tone } from '@/shared/ui';
import { ECOM_STATUS } from '../constants';
import type { EcomOrder, EcomReceiver } from '../types';

/** "inbox" = tab Đơn hàng (đơn mới về, chưa xác nhận) · "mine" = trang Đơn hàng E-com (đã xác nhận gửi, không ghi MaVanDon). */
export type OrderScope = 'inbox' | 'mine';

/** Nhóm lọc nhanh — theo việc khách cần làm tiếp ở từng tab. */
export type OrderView = 'all' | 'needsInfo' | 'ready' | 'waiting' | 'billed' | 'exception';

export const ORDER_VIEWS: Record<OrderScope, ReadonlyArray<{ key: OrderView; label: string }>> = {
  inbox: [
    { key: 'all', label: 'Tất cả' },
    { key: 'needsInfo', label: 'Cần bổ sung' },
    { key: 'ready', label: 'Sẵn sàng gửi' }
  ],
  mine: [
    { key: 'all', label: 'Tất cả' },
    { key: 'waiting', label: 'Chờ tạo bill' },
    { key: 'billed', label: 'Đã có bill' },
    { key: 'exception', label: 'Lỗi cần xử lý' }
  ]
};

const hasIssues = (o: EcomOrder) => (o.issues?.length ?? 0) > 0;

const MATCH: Record<OrderView, (o: EcomOrder) => boolean> = {
  all: () => true,
  needsInfo: o => !o.bill && hasIssues(o),
  ready: o => !o.bill && !hasIssues(o),
  waiting: o => !o.bill && o.st !== 'exception',
  billed: o => !!o.bill,
  exception: o => o.st === 'exception'
};

export const filterByView = (orders: ReadonlyArray<EcomOrder>, view: OrderView): EcomOrder[] => orders.filter(MATCH[view]);

/** Số đơn của từng nhóm trong tab. */
export function countByView(orders: ReadonlyArray<EcomOrder>, scope: OrderScope): Partial<Record<OrderView, number>> {
  return Object.fromEntries(ORDER_VIEWS[scope].map(v => [v.key, filterByView(orders, v.key).length]));
}

/**
 * Nhãn trạng thái: đơn có bill / lỗi theo trạng thái vận chuyển; đơn đã xác nhận chưa có bill là "Đã xác nhận gửi";
 * đơn mới về thì "Cần bổ sung" (thiếu trường bắt buộc) hoặc "Sẵn sàng gửi".
 */
export function displayStatus(o: Pick<EcomOrder, 'st' | 'bill' | 'confirmed' | 'issues'>): { label: string; tone: Tone } {
  if (o.bill || o.st === 'exception') return ECOM_STATUS[o.st] ?? { label: o.st, tone: 'neutral' };
  if (o.confirmed) return { label: 'Đã xác nhận gửi', tone: 'success' };
  return (o.issues?.length ?? 0) > 0 ? { label: 'Cần bổ sung', tone: 'warning' } : { label: 'Sẵn sàng gửi', tone: 'info' };
}

/** Các dòng địa chỉ người nhận như trên nhãn: đường → thành phố, bang, mã bưu chính → nước. */
export function receiverLines(r: EcomReceiver | undefined, fallbackCountry: string): string[] {
  if (!r) return fallbackCountry ? [fallbackCountry] : [];
  const cityLine = [r.city, r.state, r.postal].filter(Boolean).join(', ');
  return [r.address1, r.address2, cityLine, r.country ?? fallbackCountry].filter((s): s is string => !!s);
}
