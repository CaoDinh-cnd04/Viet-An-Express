import { formatViDate } from '@/shared/lib/format';
import { DEFAULT_TRANSIT_DAYS, TRANSIT_DAYS } from '../constants';
import type { Order } from '../types';

/** 'dd/mm/yyyy[ hh:mm]' → Date; null nếu sai định dạng. */
export function parseViDate(value?: string | null): Date | null {
  const m = (value ?? '').match(/(\d{2})\/(\d{2})\/(\d{4})(?:\s+(\d{2}):(\d{2}))?/);
  if (!m) return null;
  return new Date(Number(m[3]), Number(m[2]) - 1, Number(m[1]), Number(m[4] ?? 0), Number(m[5] ?? 0));
}

/** Ngày giao dự kiến = ngày gửi + số ngày vận chuyển tham khảo của hãng. Rỗng nếu đã phát hoặc chưa gửi. */
export function estimatePodDate(order: Pick<Order, 'pod' | 'sent' | 'route'>): string {
  if (order.pod) return '';
  const sent = parseViDate(order.sent);
  if (!sent) return '';
  const days = TRANSIT_DAYS.find(([prefix]) => order.route.startsWith(prefix))?.[1] ?? DEFAULT_TRANSIT_DAYS;
  sent.setDate(sent.getDate() + days);
  return formatViDate(sent);
}

/** Tách ngày khỏi chuỗi 'dd/mm/yyyy hh:mm'. */
export const datePart = (value: string): string => value.split(' ')[0] ?? value;
