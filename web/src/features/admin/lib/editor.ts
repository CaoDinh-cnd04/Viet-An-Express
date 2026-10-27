import { fill } from '@/shared/i18n';
import { editorValue, plainToHtml, toPlainText } from '@/features/notifications';
import type { AdminNotification, NotificationAudience, NotificationAudienceKind, SaveNotificationBody } from '../api';

/** Giới hạn — khớp backend (Domain/Notification.cs, NotificationRules). */
export const LIMITS = { title: 200, body: 4000, detail: 20000, items: 10, images: 5, link: 500, imageBytes: 2 * 1024 * 1024, imageWidth: 1600 } as const;

export interface DraftItem {
  /** Khóa React ổn định khi đổi thứ tự. */
  key: string;
  title: string;
  body: string;
  /** Tên tệp ảnh đã tải lên. */
  images: string[];
  /** Nút "Xem chi tiết": không có / mở link / mở nội dung chi tiết soạn sẵn. */
  action: ItemAction;
  link: string;
  /** HTML trình soạn của nội dung chi tiết. */
  detail: string;
}

export type ItemAction = 'none' | 'link' | 'detail';

export interface Draft {
  id: string | null;
  title: string;
  items: DraftItem[];
  important: boolean;
  pinned: boolean;
  sendEmail: boolean;
  audience: NotificationAudienceKind;
  customerTypes: number[];
  /** Mã khách nhập tay, cách nhau dấu phẩy / xuống dòng. */
  customerCodes: string;
  schedule: boolean;
  /** 'yyyy-MM-ddTHH:mm' */
  publishAt: string;
  /** 'yyyy-MM-dd' hoặc '' */
  expiresOn: string;
}

let seq = 0;
/** Không dùng crypto.randomUUID: portal chạy được trên HTTP (không phải secure context). */
export const newKey = () => `tin-${++seq}`;

/** body / detail là HTML của trình soạn có định dạng. */
export const emptyItem = (): DraftItem => ({ key: newKey(), title: '', body: '', images: [], action: 'none', link: '', detail: '' });

const pad = (n: number) => String(n).padStart(2, '0');
/** Date → 'yyyy-MM-ddTHH:mm' (giờ máy, dùng cho ô datetime-local). */
export const toLocalInput = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

/** Mặc định hẹn giờ: 07:00 sáng hôm sau. */
export function emptyDraft(now: Date): Draft {
  const tomorrow = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1, 7, 0);
  return {
    id: null, title: '', items: [emptyItem()], important: false, pinned: false, sendEmail: false,
    audience: 'all', customerTypes: [], customerCodes: '', schedule: false, publishAt: toLocalInput(tomorrow), expiresOn: ''
  };
}

/** Thông báo có sẵn → bản soạn (sửa). Đã gửi email thì không bật lại gửi email. */
export function draftFrom(n: AdminNotification, now: Date): Draft {
  return {
    ...emptyDraft(now),
    id: n.id,
    title: n.title,
    items: n.items.map(i => ({
      key: newKey(), title: i.title ?? '', body: plainToHtml(i.body), images: [...i.images],
      action: i.detail ? 'detail' : i.link ? 'link' : 'none', link: i.link ?? '', detail: i.detail ? plainToHtml(i.detail) : ''
    })),
    important: n.important,
    pinned: n.pinned,
    sendEmail: n.emailStatus !== 'none',
    audience: n.audience,
    customerTypes: [...n.customerTypes],
    customerCodes: n.customerCodes.join(', '),
    schedule: n.status === 'scheduled',
    publishAt: n.status === 'scheduled' ? n.publishAt : emptyDraft(now).publishAt,
    expiresOn: n.expiresOn ?? ''
  };
}

/**
 * Nhân bản thông báo → bản soạn mới (chưa lưu): giữ nội dung, ảnh, đối tượng nhận, quan trọng / ghim;
 * bỏ hẹn giờ, ngày hết hiện và gửi email (soạn mới chọn lại). Ảnh dùng chung tệp với bản gốc (xóa thông báo không xóa ảnh).
 */
export function copyDraft(n: AdminNotification, now: Date): Draft {
  const d = draftFrom(n, now);
  return {
    ...d,
    id: null,
    title: fill('{title} (bản sao)', { title: n.title }).slice(0, LIMITS.title),
    sendEmail: false,
    schedule: false,
    publishAt: emptyDraft(now).publishAt,
    expiresOn: ''
  };
}

/** Email đã (đang) gửi → không đổi được lựa chọn gửi email. */
export const emailLocked = (n: AdminNotification | null) => !!n && !['none', 'scheduled'].includes(n.emailStatus);

/** Mã khách nhập tay → danh sách không trùng (không phân biệt hoa thường) — khớp NotificationRules.CustomerCodes. */
export function parseCodes(text: string): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const code of text.split(/[\s,;]+/).map(c => c.trim()).filter(Boolean)) {
    const k = code.toLowerCase();
    if (!seen.has(k)) {
      seen.add(k);
      out.push(code);
    }
  }
  return out;
}

const hasLink = (i: DraftItem) => i.action === 'link' && !!i.link.trim();
const hasDetail = (i: DraftItem) => i.action === 'detail' && !!toPlainText(editorValue(i.detail));
const isBlank = (i: DraftItem) => !i.title.trim() && !toPlainText(editorValue(i.body)) && !i.images.length && !hasLink(i) && !hasDetail(i);

const webLink = (v: string) => {
  try {
    const u = new URL(v.trim());
    return (u.protocol === 'http:' || u.protocol === 'https:') && !!u.hostname && v.trim().length <= LIMITS.link;
  } catch {
    return false;
  }
};

/** Bản soạn → body gửi API (bỏ tin trống hoàn toàn). */
export function toRequest(d: Draft, publish: boolean): SaveNotificationBody {
  return {
    title: d.title.trim(),
    items: d.items.filter(i => !isBlank(i)).map(i => ({
      title: i.title.trim() || null,
      body: editorValue(i.body),
      images: i.images,
      link: hasLink(i) ? i.link.trim() : null,
      detail: hasDetail(i) ? editorValue(i.detail) : null
    })),
    important: d.important,
    pinned: d.pinned,
    sendEmail: d.sendEmail,
    audience: d.audience,
    customerTypes: d.audience === 'type' ? d.customerTypes : [],
    customerCodes: d.audience === 'pick' ? parseCodes(d.customerCodes) : [],
    publishAt: d.schedule ? d.publishAt : null,
    expiresOn: d.expiresOn || null,
    publish
  };
}

/**
 * Kiểm tra trước khi gửi (backend kiểm lại) → câu báo lỗi tiếng Việt đã điền số (component dịch bằng t), hợp lệ → null.
 * Lưu nháp chỉ cần tiêu đề; đăng cần đủ nội dung, đối tượng nhận và giờ hợp lệ.
 */
export function validateDraft(d: Draft, publish: boolean, now: Date): string | null {
  if (!d.title.trim()) return 'Nhập tiêu đề thông báo';
  if (d.title.trim().length > LIMITS.title) return fill('Tiêu đề tối đa {n} ký tự', { n: LIMITS.title });
  const items = d.items.filter(i => !isBlank(i));
  if (!publish && !items.length) return null;
  if (!items.length) return 'Viết nội dung cho ít nhất 1 tin';
  if (items.length > LIMITS.items) return fill('Tối đa {n} tin trong 1 thông báo', { n: LIMITS.items });
  for (const [n, i] of items.entries()) {
    if (items.length > 1 && !i.title.trim()) return fill('Tin {n}: nhập tiêu đề tin', { n: n + 1 });
    const text = toPlainText(editorValue(i.body));
    if (!text) return fill('Tin {n}: nhập nội dung', { n: n + 1 });
    if (text.length > LIMITS.body) return fill('Tin {n}: nội dung tối đa {max} ký tự', { n: n + 1, max: LIMITS.body });
    if (i.action === 'link' && !webLink(i.link)) return fill('Tin {n}: link phải bắt đầu bằng http:// hoặc https://', { n: n + 1 });
    if (i.action === 'detail' && !hasDetail(i)) return fill('Tin {n}: soạn nội dung chi tiết hoặc bỏ nút Xem chi tiết', { n: n + 1 });
    if (toPlainText(editorValue(i.detail)).length > LIMITS.detail) return fill('Tin {n}: nội dung chi tiết tối đa {max} ký tự', { n: n + 1, max: LIMITS.detail });
  }
  if (!publish) return null;
  if (d.audience === 'type' && !d.customerTypes.length) return 'Chọn ít nhất 1 loại khách';
  if (d.audience === 'pick' && !parseCodes(d.customerCodes).length) return 'Nhập mã khách nhận thông báo';
  const at = d.schedule ? new Date(d.publishAt) : now;
  if (d.schedule && (Number.isNaN(at.getTime()) || at <= now)) return 'Giờ hẹn đăng phải sau thời điểm hiện tại';
  if (d.expiresOn && new Date(`${d.expiresOn}T23:59:59`) < at) return 'Ngày hết hiện phải sau giờ đăng';
  return null;
}

/** Số khách sẽ nhận theo lựa chọn hiện tại. */
export function recipientsOf(d: Draft, audience: NotificationAudience | undefined): number {
  if (d.audience === 'all') return audience?.total ?? 0;
  if (d.audience === 'type') return (audience?.types ?? []).filter(t => d.customerTypes.includes(t.id)).reduce((sum, t) => sum + t.count, 0);
  return parseCodes(d.customerCodes).length;
}

export function summaryOf(d: Draft): { items: number; images: number; links: number; details: number } {
  const items = d.items.filter(i => !isBlank(i));
  return {
    items: items.length,
    images: items.reduce((n, i) => n + i.images.length, 0),
    links: items.filter(hasLink).length,
    details: items.filter(hasDetail).length
  };
}

/** Đổi chỗ phần tử (dùng cho tin và ảnh). Ngoài phạm vi → giữ nguyên. */
export function move<T>(list: readonly T[], from: number, to: number): T[] {
  if (from === to || to < 0 || to >= list.length || from < 0 || from >= list.length) return [...list];
  const next = [...list];
  const [it] = next.splice(from, 1);
  next.splice(to, 0, it!);
  return next;
}

/** Kích thước ảnh sau khi thu nhỏ vừa chiều rộng tối đa (giữ tỉ lệ, không phóng to). */
export function fitWidth(width: number, height: number, max: number): { width: number; height: number } {
  if (width <= max) return { width, height };
  return { width: max, height: Math.round((height * max) / width) };
}
