import type { TroubleKind, TroubleTicket } from '../types';

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
