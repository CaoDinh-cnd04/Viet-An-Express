import type { TroubleStatus, TroubleTicket } from '../types';

/** Bộ lọc danh sách hội thoại sự cố. */
export type TroubleFilter = 'open' | 'all' | TroubleStatus;

/** Lọc theo trạng thái + tìm theo mã ticket / mã vận đơn / người nhận / loại sự cố (không phân biệt hoa thường). */
export function filterTickets(list: ReadonlyArray<TroubleTicket>, filter: TroubleFilter, query: string): TroubleTicket[] {
  const q = query.trim().toLowerCase();
  return list.filter(t => {
    const byStatus = filter === 'all' ? true : filter === 'open' ? t.status !== 'done' : t.status === filter;
    if (!byStatus) return false;
    if (!q) return true;
    return [t.id, t.bill, t.cnee, t.type].some(v => v?.toLowerCase().includes(q));
  });
}

/** Tin cuối cùng của hội thoại để xem trước trong danh sách; chưa có trao đổi thì lấy mô tả ban đầu. */
export function lastActivity(t: TroubleTicket): { from: 'customer' | 'vietan'; text: string; date: string } {
  const last = [...(t.messages ?? [])].reverse().find(m => m.message.trim().length > 0);
  return last ? { from: last.from, text: last.message, date: last.date } : { from: 'customer', text: t.desc, date: t.date };
}
