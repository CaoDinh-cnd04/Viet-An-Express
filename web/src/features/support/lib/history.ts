import type { SupportTicket } from '../types';

/** 1 mốc trong lịch sử yêu cầu: khách gửi, CS trả lời hoặc khách trả lời lại. */
export type HistoryEntry =
  | { at: string; kind: 'sent' }
  | { at: string; kind: 'cs' | 'customer'; text: string };

/** Lịch sử của 1 yêu cầu, mới nhất trước: các lần trả lời qua lại rồi tới lúc khách gửi. Hàm thuần — có test. */
export function historyOf(ticket: SupportTicket): HistoryEntry[] {
  const replies = (ticket.thread ?? []).map((m): HistoryEntry => ({ at: m.at, kind: m.from, text: m.text }));
  return [...replies.reverse(), { at: ticket.date, kind: 'sent' }];
}

/**
 * Số tin mới của bên kia mà người xem chưa trả lời: các tin cuối liên tiếp không phải của người xem
 * (khách xem → tin CS; CS xem → tin khách, kể cả nội dung khách gửi lúc tạo). Yêu cầu đã xử lý không đánh dấu. Hàm thuần — có test.
 */
export function unansweredCount(ticket: SupportTicket, viewer: 'customer' | 'cs'): number {
  if (ticket.status === 'done') return 0;
  const mine = viewer === 'cs' ? 'cs' : 'customer';
  let n = 0;
  for (const h of historyOf(ticket)) {
    if ((h.kind === 'sent' ? 'customer' : h.kind) === mine) break;
    n++;
  }
  return n;
}

/** Tin gần nhất để xem nhanh trên thẻ: lần trả lời cuối (CS / khách), chưa ai trả lời thì nội dung khách gửi. */
export function lastMessage(ticket: SupportTicket): { from: 'cs' | 'customer'; text: string } {
  const last = ticket.thread?.at(-1);
  return last ? { from: last.from, text: last.text } : { from: 'customer', text: ticket.desc };
}
