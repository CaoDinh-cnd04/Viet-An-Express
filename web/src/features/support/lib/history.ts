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

/** CS đang chờ khách trả lời → khách được gửi phản hồi trên yêu cầu này. */
export const canAnswer = (ticket: Pick<SupportTicket, 'status'>): boolean => ticket.status === 'waitc';
