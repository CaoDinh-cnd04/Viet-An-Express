import { formatViDate, relativeDayLabel } from '@/shared/lib/format';
import { stripDiacritics } from '@/shared/lib/text';
import { toPlainText } from './richText';

/** Tin tối thiểu để hiển thị (khớp NotificationItem ở api.ts). */
export interface InboxPart {
  title?: string | null;
  body: string;
  images: string[];
  link?: string | null;
  /** Nội dung chi tiết (HTML) mở bằng nút "Xem chi tiết". */
  detail?: string | null;
}

/** Thông báo tối thiểu để phân loại / lọc (khớp Notification ở api.ts). */
export interface InboxItem {
  imp: boolean;
  pin: boolean;
  unread: boolean;
  kind: 'notice' | 'support';
  title: string;
  /** 'dd/MM/yyyy HH:mm' */
  date: string;
  body: string;
  link?: string | null;
  items: InboxPart[];
}

/** Tất cả / Chưa đọc / Quan trọng: thông báo Việt An đăng · Hỗ trợ & sự cố: cập nhật gửi riêng khách. */
export type InboxFilter = 'all' | 'unread' | 'important' | 'support';

/** Các tin của thông báo; thông báo cũ / hỗ trợ (không có tin con) → 1 tin từ body + link. */
export const partsOf = (n: Pick<InboxItem, 'body' | 'link' | 'items'>): InboxPart[] =>
  n.items.length ? n.items : [{ title: null, body: n.body, images: [], link: n.link ?? null }];

/** Dòng tóm tắt trong danh sách: nhiều tin → tiêu đề các tin; 1 tin → nội dung trên 1 dòng. */
export function excerpt(n: Pick<InboxItem, 'body' | 'link' | 'items'>): string {
  const parts = partsOf(n);
  return parts.length > 1
    ? parts.map(p => p.title?.trim() || '…').join(' · ')
    : toPlainText(parts[0]?.body).replace(/\s+/g, ' ').trim();
}

export const imagesOf = (n: Pick<InboxItem, 'body' | 'link' | 'items'>): string[] => partsOf(n).flatMap(p => p.images);

/** Mã yêu cầu trong tiêu đề thông báo hỗ trợ ("Sự cố TRB-2: Đã xử lý" → "TRB-2"), không có → null. */
export const ticketIdOf = (title: string): string | null => /trb-\d+/i.exec(title)?.[0]?.toUpperCase() ?? null;

const norm = (s: string) => stripDiacritics(s).toLowerCase();

export function matchesFilter(n: InboxItem, filter: InboxFilter): boolean {
  if (filter === 'support') return n.kind === 'support';
  if (n.kind !== 'notice') return false;
  return filter === 'all' || (filter === 'unread' ? n.unread : n.imp);
}

/** Lọc theo nhóm + tìm trong tiêu đề / nội dung các tin (không dấu). */
export function filterInbox<T extends InboxItem>(items: readonly T[], filter: InboxFilter, query: string): T[] {
  const q = norm(query.trim());
  return items.filter(n => {
    if (!matchesFilter(n, filter)) return false;
    if (!q) return true;
    const text = [n.title, ...partsOf(n).flatMap(p => [p.title ?? '', toPlainText(p.body), toPlainText(p.detail)])].join(' ');
    return norm(text).includes(q);
  });
}

/** Số thông báo trên từng nút lọc: Tất cả = số thông báo; Chưa đọc / Hỗ trợ = số chưa đọc. */
export function filterCounts(items: readonly InboxItem[]): Record<InboxFilter, number> {
  const notices = items.filter(n => n.kind === 'notice');
  return {
    all: notices.length,
    unread: notices.filter(n => n.unread).length,
    important: notices.filter(n => n.imp).length,
    support: items.filter(n => n.kind === 'support' && n.unread).length
  };
}

/**
 * Tin hiện trong popup chuông: tin chưa đọc trước (giữ thứ tự mới nhất), còn chỗ thì thêm tin đã đọc —
 * tin chưa đọc cũ không bị tin đã đọc mới hơn đẩy ra ngoài (badge đếm tin nào thì popup thấy tin đó).
 */
export function bellItems<T extends InboxItem>(items: readonly T[], limit: number): T[] {
  const unread = items.filter(n => n.unread);
  return [...unread, ...items.filter(n => !n.unread)].slice(0, Math.max(limit, 0));
}

/** Ngày 'dd/MM/yyyy' của thông báo. */
const dayOf = (date: string) => date.trim().slice(0, 10);

/** Giờ ngắn trong danh sách: hôm nay → "HH:mm", ngày khác → "dd/MM". */
export const shortTime = (date: string, today: Date) => (dayOf(date) === formatViDate(today) ? date.slice(11, 16) : date.slice(0, 5));

/** Nhóm giữ thứ tự: thông báo ghim → "Đã ghim"; còn lại "Hôm nay", "Hôm qua", hoặc ngày dd/MM/yyyy. */
export function groupByDay<T extends InboxItem>(items: readonly T[], today: Date): Array<{ label: string; items: T[] }> {
  const label = (n: T) => (n.pin && n.kind === 'notice' ? 'Đã ghim' : relativeDayLabel(dayOf(n.date), today));
  const groups: Array<{ label: string; items: T[] }> = [];
  for (const n of items) {
    const l = label(n);
    const last = groups[groups.length - 1];
    if (last?.label === l) last.items.push(n);
    else groups.push({ label: l, items: [n] });
  }
  return groups;
}
