import type { NewTrouble, TroubleKind, TroubleStatus, TroubleTicket } from '../types';
import { kindOf, ticketTitle } from './chat';

/**
 * Hỗ trợ & góp ý dạng trò chuyện (dữ liệu dbo.TContact): mỗi yêu cầu là 1 cuộc trò chuyện.
 * Tin của khách = nội dung gửi; tin của CS = thông báo riêng CS gửi khách (kind "support", tiêu đề có mã TRB-…) — hệ thống cũ không có
 * bảng tin nhắn qua lại nên khách không nhắn tiếp được. Hàm thuần — có test.
 */

/** Thông báo tối thiểu để dựng tin của CS (khớp Notification ở features/notifications). */
export interface SupportNotice {
  id: number | string;
  unread: boolean;
  kind: 'notice' | 'support';
  title: string;
  /** 'dd/MM/yyyy HH:mm' */
  date: string;
  body: string;
}

/** Tin trong khung chat. `text` là câu tiếng Việt (giao diện bọc t() với `params`). */
export type ChatMessage =
  | { type: 'day'; day: string }
  | { type: 'system'; text: string; params?: Record<string, string | number>; at: string }
  | { type: 'me'; text: string; at: string }
  | { type: 'cs'; text: string; at: string };

/** Mục lọc danh sách: Chưa đọc (CS vừa trả lời) / Đang mở / Đã đóng / Tất cả. */
export type ConversationFilter = 'unread' | 'open' | 'closed' | 'all';

export const isOpen = (t: Pick<TroubleTicket, 'status'>): boolean => t.status !== 'done';

/** Mã TRB-… trong tiêu đề thông báo hỗ trợ. */
export const noticeTicketId = (title: string): string | null => /\btrb-\d+/i.exec(title)?.[0]?.toUpperCase() ?? null;

/** 'dd/MM/yyyy HH:mm' → số để so sánh (sai định dạng → 0). */
export function timeValue(value: string | undefined): number {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})(?:\s+(\d{2}):(\d{2}))?/.exec(value?.trim() ?? '');
  return m ? Date.UTC(+m[3]!, +m[2]! - 1, +m[1]!, +(m[4] ?? 0), +(m[5] ?? 0)) : 0;
}

/** Thông báo hỗ trợ theo mã yêu cầu, cũ trước mới sau. */
export function noticesByTicket(notices: readonly SupportNotice[]): Map<string, SupportNotice[]> {
  const map = new Map<string, SupportNotice[]>();
  for (const n of notices) {
    const id = n.kind === 'support' ? noticeTicketId(n.title) : null;
    if (!id) continue;
    map.set(id, [...(map.get(id) ?? []), n]);
  }
  for (const list of map.values()) list.sort((a, b) => timeValue(a.date) - timeValue(b.date));
  return map;
}

/** Số tin CS chưa đọc của 1 yêu cầu. */
export const unreadOf = (id: string, byTicket: Map<string, SupportNotice[]>): number =>
  (byTicket.get(id) ?? []).filter(n => n.unread).length;

const STATUS_ONLY = /^Trạng thái đã chuyển sang\s+"?([^"]+?)"?\.?$/;

/**
 * Nội dung CS gửi trong thông báo: phần sau dòng trống đầu tiên (trước đó là tóm tắt yêu cầu — xem TroubleRules.CustomerNotice ở backend).
 * Chỉ báo đổi trạng thái → `status` = nhãn trạng thái mới.
 */
export function replyOf(body: string): { text: string; status: string | null } {
  const cut = body.indexOf('\n\n');
  const text = (cut >= 0 ? body.slice(cut + 2) : body).trim();
  const status = STATUS_ONLY.exec(text)?.[1] ?? null;
  return { text, status };
}

const KIND_SENT: Record<TroubleKind, string> = {
  su_co: 'Bạn đã báo sự cố "{type}" · vận đơn {bill}',
  ho_tro: 'Bạn đã gửi yêu cầu hỗ trợ "{type}"',
  gop_y: 'Bạn đã gửi góp ý'
};

/** Yêu cầu hỗ trợ có mã vận đơn → nhắc cả vận đơn. */
const sentText = (t: TroubleTicket) =>
  kindOf(t) === 'ho_tro' && t.bill ? 'Bạn đã gửi yêu cầu "{type}" · vận đơn {bill}' : KIND_SENT[kindOf(t)];

/** Tin trong khung chat của 1 yêu cầu: tin hệ thống lúc gửi, nội dung khách gửi, rồi các phản hồi / đổi trạng thái của CS — chèn mốc ngày. */
export function messagesOf(t: TroubleTicket, notices: readonly SupportNotice[]): ChatMessage[] {
  type Msg = Exclude<ChatMessage, { type: 'day' }>;
  const raw: Msg[] = [
    { type: 'system', text: sentText(t), params: { type: t.type, bill: t.bill }, at: t.date },
    { type: 'me', text: t.desc, at: t.date },
    { type: 'system', text: 'Mã yêu cầu {code} — CS phản hồi trong giờ làm việc', params: { code: t.id }, at: t.date }
  ];
  for (const n of notices) {
    const reply = replyOf(n.body);
    if (reply.status) raw.push({ type: 'system', text: 'Đã chuyển sang "{status}"', params: { status: reply.status }, at: n.date });
    else if (reply.text) raw.push({ type: 'cs', text: reply.text, at: n.date });
  }
  const out: ChatMessage[] = [];
  let day = '';
  for (const m of raw) {
    const d = m.at.trim().slice(0, 10);
    if (d && d !== day) {
      out.push({ type: 'day', day: d });
      day = d;
    }
    out.push(m);
  }
  return out;
}

/** Lần hoạt động gần nhất: tin CS mới nhất hoặc lần đổi trạng thái / gửi. */
export function lastActivity(t: TroubleTicket, notices: readonly SupportNotice[]): number {
  return Math.max(timeValue(t.updatedAt ?? t.date), timeValue(t.date), ...notices.map(n => timeValue(n.date)));
}

/** Dòng xem trước trong danh sách: tin CS mới nhất, không có thì nội dung khách gửi. */
export function previewOf(t: TroubleTicket, notices: readonly SupportNotice[]): { from: 'cs' | 'me'; text: string } {
  const last = [...notices].reverse().map(n => replyOf(n.body)).find(r => r.text && !r.status);
  return last ? { from: 'cs', text: last.text } : { from: 'me', text: t.desc };
}

/** Mục mở đầu: Chưa đọc nếu có tin CS chưa đọc; không có thì Đang mở; không còn cuộc nào mở thì Tất cả. */
export function defaultFilter(tickets: readonly TroubleTicket[], byTicket: Map<string, SupportNotice[]>): ConversationFilter {
  if (tickets.some(t => unreadOf(t.id, byTicket) > 0)) return 'unread';
  return tickets.some(isOpen) ? 'open' : 'all';
}

/**
 * Lọc + tìm (mã, vận đơn, người nhận, loại, tiêu đề, nội dung), mới hoạt động trước.
 * `keep` = cuộc vừa mở trong mục Chưa đọc — vẫn giữ trong danh sách tới khi đổi mục (như Gmail).
 */
export function filterConversations(
  tickets: readonly TroubleTicket[],
  byTicket: Map<string, SupportNotice[]>,
  filter: ConversationFilter,
  query: string,
  keep: string | null = null
): TroubleTicket[] {
  const q = query.trim().toLowerCase();
  return tickets
    .filter(t =>
      filter === 'all' ? true
        : filter === 'unread' ? unreadOf(t.id, byTicket) > 0 || t.id === keep
          : filter === 'open' ? isOpen(t) : !isOpen(t))
    .filter(t => !q || [t.id, t.bill, t.cnee, t.type, t.subject, t.desc].some(v => v?.toLowerCase().includes(q)))
    .sort((a, b) => lastActivity(b, byTicket.get(b.id) ?? []) - lastActivity(a, byTicket.get(a.id) ?? []));
}

/** Số cuộc trên từng mục lọc. */
export function filterCounts(tickets: readonly TroubleTicket[], byTicket: Map<string, SupportNotice[]>): Record<ConversationFilter, number> {
  return {
    unread: tickets.filter(t => unreadOf(t.id, byTicket) > 0).length,
    open: tickets.filter(isOpen).length,
    closed: tickets.filter(t => !isOpen(t)).length,
    all: tickets.length
  };
}

/** Nội dung soạn trong hộp thoại "Cuộc trò chuyện mới". */
export interface ConversationDraft {
  kind: TroubleKind;
  bill: string;
  /** Vấn đề (HELP_TOPICS) — hỗ trợ & sự cố; nội dung góp ý (FEEDBACK_TOPICS) — góp ý. */
  topic: string;
  /** Mức hài lòng 1–5 (góp ý). */
  rating: number | null;
  text: string;
  urgent: boolean;
  /** Người liên hệ + SĐT / email — lấy từ tài khoản. */
  requester: string;
  contact: string;
}

/** Chữ ghi vào nội dung gửi CS (dữ liệu CS đọc ở phần mềm cũ — không dịch, xem NO_TRANSLATE). */
const DATA = { bill: 'vận đơn', rating: 'Mức hài lòng', feedback: 'Góp ý' } as const;

/** Khớp backend (TroubleRules.Validate). */
export const DESC_MIN = 10;

/** Lỗi đầu tiên (câu tiếng Việt) hoặc null — nút Gửi chỉ sáng khi hợp lệ. */
export function draftError(d: ConversationDraft): string | null {
  if (!d.topic) return d.kind === 'gop_y' ? 'Chọn nội dung góp ý' : 'Chọn vấn đề cần hỗ trợ';
  // Có mã vận đơn thì nội dung không bắt buộc (tự ghi "{vấn đề} — vận đơn {mã}").
  if (!d.text.trim() && (d.kind === 'gop_y' || !d.bill.trim())) return d.kind === 'gop_y' ? 'Viết góp ý của bạn' : 'Viết nội dung cần hỗ trợ';
  if (d.kind !== 'gop_y' && !d.contact.trim()) return 'Nhập SĐT hoặc email để CS liên hệ';
  return null;
}

/**
 * Hộp thoại → POST /troubles. Hỗ trợ & sự cố: mã vận đơn không bắt buộc; nội dung ngắn / trống thì ghép vấn đề (+ vận đơn) cho đủ
 * ≥ 10 ký tự backend cần; tiêu đề = vấn đề (như form "Yêu cầu" cũ). Góp ý: mức hài lòng ghi đầu nội dung để CS đọc ở phần mềm cũ.
 * Gấp → mức độ "Rất gấp", không thì "Thường".
 */
export function toNewTrouble(d: ConversationDraft, file: File | null): NewTrouble {
  const text = d.text.trim();
  const bill = d.bill.trim();
  if (d.kind !== 'gop_y') {
    const head = bill ? `${d.topic} — ${DATA.bill} ${bill}` : d.topic;
    const desc = text.length >= DESC_MIN ? text : [head, text].filter(Boolean).join('. ');
    return { kind: 'ho_tro', bill: bill || undefined, type: d.topic, lv: d.urgent ? 'high' : 'low', desc, req: d.requester.trim(), contact: d.contact.trim(), file };
  }
  const rated = d.rating ? `${DATA.rating}: ${d.rating}/5\n${text}` : text;
  const desc = rated.length >= DESC_MIN ? rated : `${DATA.feedback}: ${rated}`;
  return { kind: 'gop_y', type: d.topic, desc, req: d.requester.trim(), contact: d.contact.trim(), file };
}

/** Trạng thái hiển thị dạng trò chuyện: "Chờ khách phản hồi" → "Cần bạn". */
export const CHAT_STATUS: Record<TroubleStatus, { label: string; tone: 'new' | 'doing' | 'wait' | 'done' }> = {
  new: { label: 'Đã gửi', tone: 'new' },
  doing: { label: 'Đang xử lý', tone: 'doing' },
  waitc: { label: 'Cần bạn', tone: 'wait' },
  done: { label: 'Đã xong', tone: 'done' }
};

/**
 * Đoạn chat ra chữ thuần để khách tải về (.txt): tiêu đề cuộc + từng tin "dd/MM/yyyy HH:mm  Bạn / CS Việt An: nội dung".
 * <paramref name="label"/> dịch tin hệ thống / nhãn người gửi theo ngôn ngữ đang dùng.
 */
export function transcriptOf(
  t: TroubleTicket,
  messages: readonly ChatMessage[],
  label: (text: string, params?: Record<string, string | number>) => string
): string {
  const head = [`${t.id} · ${label(ticketTitle(t))}${t.bill ? ` · ${t.bill}` : ''}`, ''];
  const lines = messages.flatMap(m => {
    if (m.type === 'day') return [];
    const who = m.type === 'me' ? label('Bạn') : m.type === 'cs' ? label('CS Việt An') : '';
    const text = m.type === 'system' ? label(m.text, m.params) : m.text;
    return [`${m.at}  ${who ? `${who}: ` : ''}${text.replace(/\n/g, '\n    ')}`];
  });
  return [...head, ...lines, ''].join('\n');
}
