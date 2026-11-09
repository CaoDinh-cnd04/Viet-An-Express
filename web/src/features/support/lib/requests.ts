import type { SupportTicket } from '../types';
import { kindOf } from './ticket';

/**
 * Danh sách yêu cầu hỗ trợ & góp ý (dbo.TContact) của khách: ô trạng thái + lọc loại / thời gian + tìm. Hàm thuần — có test.
 */

/** Ô trạng thái đầu trang: CS chờ bạn trả lời / đang xử lý (mới + đang xử lý) / đã xử lý / tất cả. */
export type TicketFilter = 'waitc' | 'doing' | 'closed' | 'all';

/** Lọc loại: hỗ trợ (gồm sự cố cũ) / góp ý; trống = tất cả. */
export type TicketKindFilter = '' | 'ho_tro' | 'gop_y';

/** Khoảng thời gian theo lần hoạt động gần nhất; trống = tất cả. */
export type TicketPeriod = '' | '7d' | '30d' | 'month';

export const isOpen = (t: Pick<SupportTicket, 'status'>): boolean => t.status !== 'done';

const inFilter = (t: SupportTicket, filter: TicketFilter): boolean =>
  filter === 'all' ? true
    : filter === 'waitc' ? t.status === 'waitc'
      : filter === 'doing' ? t.status === 'new' || t.status === 'doing'
        : t.status === 'done';

/** 'dd/MM/yyyy HH:mm' → số để so sánh (sai định dạng → 0). */
export function timeValue(value: string | undefined): number {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})(?:\s+(\d{2}):(\d{2}))?/.exec(value?.trim() ?? '');
  return m ? Date.UTC(+m[3]!, +m[2]! - 1, +m[1]!, +(m[4] ?? 0), +(m[5] ?? 0)) : 0;
}

/** 'dd/MM/yyyy HH:mm' gọn cho bảng: cùng năm → 'dd/MM HH:mm', khác năm → 'dd/MM/yyyy'. */
export function compactTime(value: string | undefined, today: Date): string {
  const m = /^(\d{2}\/\d{2})\/(\d{4})(?:\s+(\d{2}:\d{2}))?/.exec(value?.trim() ?? '');
  if (!m) return value ?? '';
  return +m[2]! === today.getFullYear() ? `${m[1]}${m[3] ? ` ${m[3]}` : ''}` : `${m[1]}/${m[2]}`;
}

/** Lần hoạt động gần nhất: CS / khách trả lời, đổi trạng thái hoặc lúc gửi. */
export const lastActivity = (t: SupportTicket): number => Math.max(timeValue(t.updatedAt ?? t.date), timeValue(t.date));

/** Mốc bắt đầu của khoảng thời gian (cùng hệ với timeValue: giờ địa phương ghi như UTC). */
export function periodStart(period: TicketPeriod, today: Date): number {
  const day = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
  if (period === '7d') return day - 6 * 86_400_000;
  if (period === '30d') return day - 29 * 86_400_000;
  if (period === 'month') return Date.UTC(today.getFullYear(), today.getMonth(), 1);
  return 0;
}

/** Ô mở đầu: có yêu cầu CS đang chờ → "Cần bạn trả lời"; còn yêu cầu đang xử lý → "Đang xử lý"; không thì "Tất cả". */
export function defaultFilter(tickets: readonly SupportTicket[]): TicketFilter {
  if (tickets.some(t => t.status === 'waitc')) return 'waitc';
  return tickets.some(isOpen) ? 'doing' : 'all';
}

/** Lần cập nhật gần nhất từ mốc `from` (periodStart) trở đi; from = 0 → mọi lúc. */
export const withinPeriod = (t: SupportTicket, from: number): boolean => !from || lastActivity(t) >= from;

export interface TicketQuery {
  filter: TicketFilter;
  kind?: TicketKindFilter;
  period?: TicketPeriod;
  query?: string;
}

/** Lọc theo ô trạng thái + loại + thời gian + tìm (mã, vận đơn, người nhận, vấn đề, tiêu đề, nội dung), mới hoạt động trước. */
export function filterTickets(tickets: readonly SupportTicket[], q: TicketQuery, today: Date = new Date()): SupportTicket[] {
  const text = q.query?.trim().toLowerCase() ?? '';
  const from = periodStart(q.period ?? '', today);
  return tickets
    .filter(t => inFilter(t, q.filter))
    .filter(t => !q.kind || (q.kind === 'gop_y' ? kindOf(t) === 'gop_y' : kindOf(t) !== 'gop_y'))
    .filter(t => withinPeriod(t, from))
    .filter(t => !text || [t.id, t.bill, t.cnee, t.type, t.subject, t.desc].some(v => v?.toLowerCase().includes(text)))
    .sort((a, b) => lastActivity(b) - lastActivity(a));
}

/** Số yêu cầu trên từng ô trạng thái. */
export function filterCounts(tickets: readonly SupportTicket[]): Record<TicketFilter, number> {
  return {
    waitc: tickets.filter(t => inFilter(t, 'waitc')).length,
    doing: tickets.filter(t => inFilter(t, 'doing')).length,
    closed: tickets.filter(t => inFilter(t, 'closed')).length,
    all: tickets.length
  };
}
