import { stripDiacritics } from '@/shared/lib/text';

/** Thông báo tối thiểu để phân loại / lọc (khớp Notification ở api.ts). */
export interface InboxItem {
  imp: boolean;
  unread: boolean;
  title: string;
  /** 'dd/MM/yyyy HH:mm' */
  date: string;
  body: string;
}

export type NoticeKind = 'trouble' | 'important' | 'general';
export type InboxFilter = 'all' | 'unread' | 'important' | 'trouble';

/** Thông báo sự cố do hệ thống tự gửi có tiêu đề "Sự cố TRB-…". */
const TROUBLE_TITLE = /^sự cố\s+trb-\d+/i;

export const noticeKind = (n: InboxItem): NoticeKind => (TROUBLE_TITLE.test(n.title.trim()) ? 'trouble' : n.imp ? 'important' : 'general');

/** Mã sự cố trong tiêu đề ("Sự cố TRB-2: Đã xử lý" → "TRB-2"), không có → null. */
export const troubleIdOf = (title: string): string | null => /trb-\d+/i.exec(title)?.[0]?.toUpperCase() ?? null;

const norm = (s: string) => stripDiacritics(s).toLowerCase();

/** Lọc theo loại + tìm trong tiêu đề / nội dung (không dấu). */
export function filterInbox<T extends InboxItem>(items: readonly T[], filter: InboxFilter, query: string): T[] {
  const q = norm(query.trim());
  return items.filter(n => {
    const ok =
      filter === 'all' ? true : filter === 'unread' ? n.unread : filter === 'important' ? n.imp : noticeKind(n) === 'trouble';
    return ok && (!q || norm(n.title).includes(q) || norm(n.body).includes(q));
  });
}

/** Ngày 'dd/MM/yyyy' của thông báo. */
const dayOf = (date: string) => date.trim().slice(0, 10);

const fmt = (d: Date) => `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;

/** Nhóm theo ngày giữ thứ tự: "Hôm nay", "Hôm qua", còn lại theo ngày dd/MM/yyyy. */
export function groupByDay<T extends InboxItem>(items: readonly T[], today: Date): Array<{ label: string; items: T[] }> {
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
  const label = (day: string) => (day === fmt(today) ? 'Hôm nay' : day === fmt(yesterday) ? 'Hôm qua' : day);
  const groups: Array<{ label: string; items: T[] }> = [];
  for (const n of items) {
    const l = label(dayOf(n.date));
    const last = groups[groups.length - 1];
    if (last?.label === l) last.items.push(n);
    else groups.push({ label: l, items: [n] });
  }
  return groups;
}
