/**
 * Nội dung có định dạng của thông báo (cỡ chữ, in đậm / nghiêng / gạch chân, màu chữ) — cùng quy tắc với backend
 * (Domain/RichText.cs): chỉ giữ thẻ và kiểu chữ cho phép, bỏ mọi thuộc tính khác, đóng đủ thẻ.
 * Không dùng DOM để chạy được cả khi test (node) — hàm thuần, có test.
 */

const TAGS = new Set(['p', 'div', 'br', 'b', 'strong', 'i', 'em', 'u', 'span', 'ul', 'ol', 'li']);
/** Bỏ cả nội dung bên trong. */
const DROPPED = new Set(['script', 'style', 'iframe', 'object', 'embed', 'template', 'noscript', 'head', 'title', 'svg', 'math']);

const TOKEN = /<!--[\s\S]*?-->|<(\/)?([a-zA-Z][a-zA-Z0-9]*)\b((?:[^>"']|"[^"]*"|'[^']*')*?)(\/)?>|[^<]+|</g;
const RICH_TAG = /<\s*\/?\s*(?:p|div|br|span|b|strong|i|em|u|ul|ol|li)\b[^>]*>/i;
const STYLE_ATTR = /\bstyle\s*=\s*("([^"]*)"|'([^']*)')/i;
const COLOR = /^(#[0-9a-f]{3}|#[0-9a-f]{6}|rgb\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\))$/;
const LONE_AMP = /&(?!(?:[a-zA-Z][a-zA-Z0-9]{1,31}|#[0-9]{1,7}|#x[0-9a-fA-F]{1,6});)/g;

/** Cỡ chữ trên thanh công cụ (px, như danh sách cỡ chữ của Word) — nằm trong khoảng backend cho phép (10 – 40 px). */
export const FONT_SIZES = [12, 13, 14, 15, 16, 18, 20, 24, 28, 32] as const;

/** Cỡ chữ hiển thị trên thanh công cụ: px của chữ tại con trỏ, làm tròn (14.5 → 15). */
export const sizeLabel = (px: number): string => (Number.isFinite(px) && px > 0 ? String(Math.round(px)) : '');

/** Tăng / giảm cỡ chữ 1 bậc theo FONT_SIZES (như Ctrl+Shift+> / < của Word); đã ở đầu / cuối danh sách thì giữ nguyên. */
export function stepFontSize(px: number, direction: 1 | -1): number {
  const sizes: readonly number[] = FONT_SIZES;
  const n = Math.round(px);
  if (direction > 0) return sizes.find(s => s > n) ?? Math.max(...sizes);
  return [...sizes].reverse().find(s => s < n) ?? Math.min(...sizes);
}

/** Màu chữ trên thanh công cụ (hex — backend nhận hex / rgb). */
export const TEXT_COLORS = ['#111827', '#6b7280', '#0b4d39', '#2e8b3e', '#1d4ed8', '#dc2626', '#c2410c', '#9333ea'] as const;

/** Có thẻ định dạng → nội dung HTML; không có → chữ thường (thông báo cũ / hệ thống). */
export const isRichText = (value?: string | null): boolean => !!value && RICH_TAG.test(value);

const ENTITIES: Record<string, string> = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', '#39': "'" };

/** Giải mã thực thể HTML thường gặp (&amp; &lt; &#123; &#x1F;…). */
export function decodeEntities(text: string): string {
  return text.replace(/&(#x[0-9a-fA-F]+|#[0-9]+|[a-zA-Z][a-zA-Z0-9]*);/g, (m, e: string) => {
    if (e[0] === '#') {
      const code = e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return Number.isFinite(code) && code > 0 && code < 0x110000 ? String.fromCodePoint(code) : m;
    }
    return ENTITIES[e] ?? m;
  }).replace(/ /g, ' ');
}

export const escapeHtml = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function cleanStyle(attrs: string): string {
  const m = STYLE_ATTR.exec(attrs);
  if (!m) return '';
  const kept: string[] = [];
  for (const decl of decodeEntities(m[2] ?? m[3] ?? '').split(';')) {
    const colon = decl.indexOf(':');
    if (colon <= 0) continue;
    const prop = decl.slice(0, colon).trim().toLowerCase();
    const value = decl.slice(colon + 1).trim().toLowerCase();
    let clean: string | null = null;
    if (prop === 'color') clean = COLOR.test(value) ? value : null;
    else if (prop === 'font-size') {
      const px = /^(\d{1,2})px$/.exec(value);
      clean = px && +px[1]! >= 10 && +px[1]! <= 40 ? value : null;
    } else if (prop === 'font-weight') clean = ['bold', 'bolder', '600', '700', '800', '900'].includes(value) ? 'bold' : null;
    else if (prop === 'font-style') clean = value === 'italic' ? 'italic' : null;
    else if (prop === 'text-decoration' || prop === 'text-decoration-line') {
      const parts = [...new Set(value.split(/\s+/).filter(p => p === 'underline' || p === 'line-through'))];
      clean = parts.length ? parts.join(' ') : null;
    }
    if (clean) kept.push(`${prop === 'text-decoration-line' ? 'text-decoration' : prop}: ${clean}`);
  }
  return kept.join('; ');
}

/** HTML → chỉ còn thẻ / kiểu chữ cho phép, đóng đủ thẻ (khớp RichText.Sanitize ở backend). */
export function sanitizeHtml(html: string): string {
  let out = '';
  const open: string[] = [];
  let skipUntil: string | null = null;
  for (const m of html.matchAll(TOKEN)) {
    const token = m[0];
    const [, close, rawName, attrs = '', self] = m;
    if (skipUntil) {
      if (close && rawName?.toLowerCase() === skipUntil) skipUntil = null;
      continue;
    }
    if (token.startsWith('<!--')) continue;
    if (!rawName) {
      out += token.replace(LONE_AMP, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
      continue;
    }
    const name = rawName.toLowerCase();
    if (close) {
      const at = open.lastIndexOf(name);
      if (at < 0) continue;
      for (let k = open.length - 1; k >= at; k--) out += `</${open[k]}>`;
      open.length = at;
      continue;
    }
    if (DROPPED.has(name)) {
      if (!self) skipUntil = name;
      continue;
    }
    if (!TAGS.has(name)) continue;
    if (name === 'br') {
      out += '<br>';
      continue;
    }
    const style = cleanStyle(attrs);
    out += `<${name}${style ? ` style="${style}"` : ''}>`;
    if (self) out += `</${name}>`;
    else open.push(name);
  }
  for (let k = open.length - 1; k >= 0; k--) out += `</${open[k]}>`;
  return out;
}

/** Chữ thuần (tìm kiếm, tóm tắt, đếm độ dài): xuống dòng ở br / hết đoạn. */
export function toPlainText(value?: string | null): string {
  if (!value) return '';
  if (!isRichText(value)) return value.trim();
  const text = decodeEntities(value.replace(/<br\s*\/?>|<\/(?:p|div|li)>/gi, '\n').replace(/<[^>]*>/g, ''));
  return text.replace(/\n{3,}/g, '\n\n').trim();
}

/** Chữ thường (thông báo cũ) → HTML để mở trong trình soạn có định dạng. */
export const plainToHtml = (text: string) => (isRichText(text) ? text : escapeHtml(text).replace(/\r?\n/g, '<br>'));

/** HTML trình soạn → giá trị lưu: chỉ có chữ → chữ thường; có định dạng → HTML đã lọc; trống → ''. */
export function editorValue(html: string): string {
  const cleaned = sanitizeHtml(html).trim();
  if (!isRichText(cleaned)) return decodeEntities(cleaned).trim();
  return toPlainText(cleaned) ? cleaned : '';
}
