import type { TroubleKind, TroubleStatus, TroubleTicket } from '../types';

/** Bộ lọc danh sách hội thoại sự cố. */
export type TroubleFilter = 'open' | 'all' | TroubleStatus;

/** Loại yêu cầu — API cũ không gửi `kind` thì là sự cố. */
export const kindOf = (t: Pick<TroubleTicket, 'kind'>): TroubleKind => t.kind ?? 'su_co';

/** Tiêu đề hiển thị: sự cố → loại sự cố; hỗ trợ → tiêu đề; góp ý → "Góp ý". */
export function ticketTitle(t: TroubleTicket): string {
  const kind = kindOf(t);
  if (kind === 'ho_tro') return t.subject?.trim() || t.type;
  if (kind === 'gop_y') return 'Góp ý';
  return t.type;
}

/** Dòng phụ: sự cố → mã vận đơn; hỗ trợ → chủ đề; góp ý → không có. */
export function ticketSubline(t: TroubleTicket): string {
  const kind = kindOf(t);
  if (kind === 'su_co') return t.bill;
  if (kind === 'ho_tro') return t.type;
  return '';
}

/** Lọc theo trạng thái + tìm theo mã ticket / mã vận đơn / người nhận / loại sự cố / tiêu đề (không phân biệt hoa thường). */
export function filterTickets(list: ReadonlyArray<TroubleTicket>, filter: TroubleFilter, query: string): TroubleTicket[] {
  const q = query.trim().toLowerCase();
  return list.filter(t => {
    const byStatus = filter === 'all' ? true : filter === 'open' ? t.status !== 'done' : t.status === filter;
    if (!byStatus) return false;
    if (!q) return true;
    return [t.id, t.bill, t.cnee, t.type, t.subject].some(v => v?.toLowerCase().includes(q));
  });
}
