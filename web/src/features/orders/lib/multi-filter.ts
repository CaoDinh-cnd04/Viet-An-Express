import type { OrderStatus } from '@/shared/model/shipment';
import type { OrderSearchField } from '../types';

export const ALL_STATUSES: readonly OrderStatus[] = ['wait', 'fly', 'nd', 'ok'];
/** Số thẻ tìm tối đa mỗi lần (dán cả cột bill). */
export const MAX_TERMS = 200;

/* ---------- Trạng thái: chọn nhiều, lưu dạng "wait,fly" (hoặc "all") ---------- */

export function parseStatuses(value: string): OrderStatus[] {
  const picked = new Set(value.split(','));
  return ALL_STATUSES.filter(s => picked.has(s));
}

/** Bật / tắt 1 trạng thái. Không chọn gì hoặc chọn đủ cả 5 → "all". */
export function toggleStatus(value: string, status: OrderStatus): string {
  const current = new Set(parseStatuses(value));
  if (current.has(status)) current.delete(status);
  else current.add(status);
  const list = ALL_STATUSES.filter(s => current.has(s));
  return list.length === 0 || list.length === ALL_STATUSES.length ? 'all' : list.join(',');
}

/* ---------- Tìm theo thẻ (chip) ---------- */

/** 1 thẻ tìm: giá trị + trường tìm lúc tạo thẻ. */
export interface SearchTag {
  field: OrderSearchField;
  value: string;
}

const FIELDS: readonly OrderSearchField[] = ['all', 'cnee', 'bill', 'ref', 'ct', 'svc'];

/** Ký tự chốt từ khóa đang gõ thành thẻ: chỉ dấu phẩy (dấu cách thuộc từ khóa, vd tên người nhận). */
export const TAG_SEPARATOR = /^,$/;

const LINE_BREAK = /\r?\n/;
const PASTE_BREAK = /[\r\n\t]+/;

const tagKey = (t: SearchTag) => `${t.field}:${t.value.toLowerCase()}`;

function dedupe(tags: readonly SearchTag[]): SearchTag[] {
  const seen = new Set<string>();
  const out: SearchTag[] = [];
  for (const t of tags) {
    if (seen.has(tagKey(t))) continue;
    seen.add(tagKey(t));
    out.push(t);
  }
  return out.slice(0, MAX_TERMS);
}

/**
 * Đọc `q` trên URL: mỗi dòng 1 thẻ dạng "field:giá trị" (vd "cnee:Ms Uyen").
 * Dòng không có tiền tố trường hợp lệ → dùng `fallback` (tương thích link cũ).
 */
export function parseTags(q: string, fallback: OrderSearchField = 'all'): SearchTag[] {
  const out: SearchTag[] = [];
  for (const line of q.split(LINE_BREAK)) {
    const m = line.match(/^(\w+):(.*)$/);
    const known = !!m && FIELDS.includes(m[1] as OrderSearchField);
    const value = (known ? m![2]! : line).trim();
    if (value) out.push({ field: known ? (m![1] as OrderSearchField) : fallback, value });
  }
  return dedupe(out);
}

/** Ghi thẻ ra `q` (cũng là dạng gửi backend): mỗi thẻ 1 dòng "field:giá trị". */
export function serializeTags(tags: readonly SearchTag[]): string {
  return tags.map(t => `${t.field}:${t.value}`).join('\n');
}

/**
 * Tách chữ khách gõ thành các từ khóa cách nhau bằng dấu phẩy; giữ dấu cách trong từ khóa
 * ("HONG AN DUONG, US" → 2 thẻ). Bỏ ngoặc kép bao quanh nếu khách quen gõ.
 */
export function tokenize(text: string): string[] {
  return text
    .split(',')
    .map(v => v.trim().replace(/^"(.*)"$/, '$1').replace(/\s+/g, ' ').trim())
    .filter(Boolean);
}

/** Thêm thẻ (bỏ trùng cùng trường, không phân biệt hoa thường; tối đa MAX_TERMS). */
export function addTags(tags: readonly SearchTag[], field: OrderSearchField, values: readonly string[]): SearchTag[] {
  return dedupe([...tags, ...values.map(value => ({ field, value }))]);
}

/** Dán từ Excel: mỗi dòng / mỗi ô là 1 thẻ (giữ khoảng trắng trong tên). Null = dán 1 dòng bình thường. */
export function pastedValues(text: string): string[] | null {
  if (!/[\r\n\t]/.test(text.trim())) return null;
  return text.split(PASTE_BREAK).map(v => v.trim()).filter(Boolean);
}
