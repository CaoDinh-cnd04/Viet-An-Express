import type { SupportTicket } from '../types';

/**
 * Danh sách yêu cầu hỗ trợ & góp ý (dbo.TContact). CS chỉ đổi trạng thái (TContact.Status) và trả lời qua điện thoại / email —
 * hệ thống cũ không có bảng tin nhắn qua lại. Hàm thuần — có test.
 */

/** Tab lọc: Chưa xong / Đã xử lý / Tất cả. */
export type TicketFilter = 'open' | 'closed' | 'all';

export const isOpen = (t: Pick<SupportTicket, 'status'>): boolean => t.status !== 'done';

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

/** Lần hoạt động gần nhất: CS đổi trạng thái hoặc lúc gửi. */
export const lastActivity = (t: SupportTicket): number => Math.max(timeValue(t.updatedAt ?? t.date), timeValue(t.date));

/** Tab mở đầu: Chưa xong; không còn yêu cầu nào mở thì Tất cả. */
export const defaultFilter = (tickets: readonly SupportTicket[]): TicketFilter => (tickets.some(isOpen) ? 'open' : 'all');

/** Lọc theo tab + tìm (mã, vận đơn, người nhận, vấn đề, tiêu đề, nội dung), mới hoạt động trước. */
export function filterTickets(tickets: readonly SupportTicket[], filter: TicketFilter, query: string): SupportTicket[] {
  const q = query.trim().toLowerCase();
  return tickets
    .filter(t => (filter === 'all' ? true : filter === 'open' ? isOpen(t) : !isOpen(t)))
    .filter(t => !q || [t.id, t.bill, t.cnee, t.type, t.subject, t.desc].some(v => v?.toLowerCase().includes(q)))
    .sort((a, b) => lastActivity(b) - lastActivity(a));
}

/** Số yêu cầu trên từng tab. */
export function filterCounts(tickets: readonly SupportTicket[]): Record<TicketFilter, number> {
  return {
    open: tickets.filter(isOpen).length,
    closed: tickets.filter(t => !isOpen(t)).length,
    all: tickets.length
  };
}
