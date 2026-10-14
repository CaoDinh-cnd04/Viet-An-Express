import type { EcomOrder } from '../types';
import { code128Svg } from './code128';
import { receiverLines } from './order-view';

/** Mẫu in cho đơn E-commerce (đơn chưa có bill Việt An — in theo mã đơn shop). */
export type PrintKind = 'label' | 'packing' | 'manifest';

export const PRINT_KINDS: ReadonlyArray<{ kind: PrintKind; label: string }> = [
  { kind: 'label', label: 'Nhãn dán kiện (A6)' },
  { kind: 'packing', label: 'Phiếu đóng gói (A4)' },
  { kind: 'manifest', label: 'Bảng kê giao hàng (A4)' }
];

export interface PrintContext {
  /** Khách đang đăng nhập — người gửi trên nhãn / bên giao trên bảng kê. */
  sender: { companyName: string; customerCode: string; phone?: string | null; address?: string | null };
  /** "dd/MM/yyyy HH:mm" */
  printedAt: string;
  t: (text: string, vars?: Record<string, string | number>) => string;
  formatNumber: (n: number) => string;
  /** "tiktok" → "TikTok Shop" */
  sourceLabel: (src: string) => string;
}

const esc = (s: unknown): string =>
  String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);

const money = (n: number | null | undefined, cur: string | null | undefined, fmt: (n: number) => string) =>
  n === null || n === undefined ? '—' : `${fmt(n)}${cur ? ` ${esc(cur)}` : ''}`;

const BASE_CSS = `
  * { box-sizing: border-box; }
  body { margin: 0; font-family: Arial, Helvetica, sans-serif; color: #000; font-size: 11pt; }
  .page { page-break-after: always; break-after: page; }
  .page:last-child { page-break-after: auto; break-after: auto; }
  table { width: 100%; border-collapse: collapse; }
  th, td { border: 1px solid #000; padding: 4px 6px; text-align: left; vertical-align: top; }
  th { font-size: 9pt; background: #f2f2f2; }
  .num { text-align: right; white-space: nowrap; }
  .muted { color: #444; font-size: 9pt; }
  h1 { font-size: 16pt; margin: 0 0 4px; }
`;

/** Nhãn dán kiện 105 × 148 mm, mỗi đơn 1 trang — chữ tiếng Anh như nhãn hãng bay quốc tế. */
function labelPage(o: EcomOrder, c: PrintContext): string {
  const r = o.receiver;
  const lines = receiverLines(r, o.ct);
  const country = r?.country ?? o.ct;
  return `<section class="page label">
    <div class="lhead"><strong>VIET AN EXPRESS</strong><span>${esc(c.sourceLabel(o.src))} · ${esc(o.createdAt)}</span></div>
    <div class="code">${code128Svg(o.ref)}<div class="ref">${esc(o.ref)}</div></div>
    <div class="box to">
      <div class="cap">Ship to</div>
      <div class="name">${esc(r?.name || o.cnee)}</div>
      ${r?.company ? `<div>${esc(r.company)}</div>` : ''}
      ${lines.slice(0, -1).map(l => `<div>${esc(l)}</div>`).join('')}
      <div class="country">${esc(country)}${r?.countryCode ? ` (${esc(r.countryCode)})` : ''}</div>
      ${r?.phone ? `<div>Tel: ${esc(r.phone)}</div>` : ''}
    </div>
    <div class="box from">
      <div class="cap">From</div>
      <div>${esc(c.sender.companyName)} · ${esc(c.sender.customerCode)}</div>
      ${c.sender.phone ? `<div>Tel: ${esc(c.sender.phone)}</div>` : ''}
    </div>
    <table class="facts"><tr>
      <td><span class="cap">Items</span><br><strong>${o.items}</strong></td>
      <td><span class="cap">Weight</span><br><strong>${o.kg ? `${c.formatNumber(o.kg)} kg` : '—'}</strong></td>
      <td><span class="cap">Value</span><br><strong>${money(o.value, o.currency, c.formatNumber)}</strong></td>
      ${o.bill ? `<td><span class="cap">VA Bill</span><br><strong>${esc(o.bill)}</strong></td>` : ''}
    </tr></table>
  </section>`;
}

const LABEL_CSS = `
  @page { size: 105mm 148mm; margin: 4mm; }
  .label { height: 139mm; display: flex; flex-direction: column; gap: 3mm; font-size: 10pt; }
  .lhead { display: flex; justify-content: space-between; font-size: 8pt; border-bottom: 2px solid #000; padding-bottom: 1mm; }
  .code svg { width: 100%; height: 16mm; display: block; }
  .ref { text-align: center; font-size: 14pt; font-weight: bold; letter-spacing: 1px; }
  .box { border: 1px solid #000; padding: 2mm; }
  .to { flex: 1; font-size: 11pt; line-height: 1.35; }
  .to .name { font-size: 13pt; font-weight: bold; }
  .to .country { font-size: 13pt; font-weight: bold; margin-top: 1mm; }
  .cap { font-size: 7pt; text-transform: uppercase; color: #333; }
  .facts td { width: 25%; }
`;

/** Phiếu đóng gói A4, mỗi đơn 1 trang — nhân viên so hàng khi đóng gói, bỏ kèm kiện. */
function packingPage(o: EcomOrder, c: PrintContext): string {
  const { t } = c;
  /** Chữ đã dịch + escape — để template chỉ còn tên biến. */
  const L = {
    k0: esc(t('Phiếu đóng gói')),
    k1: esc(t('In lúc {time}', { time: c.printedAt })),
    k2: esc(t('Người nhận')),
    k3: esc(t('Ngày đặt')),
    k4: esc(t('Nguồn')),
    k5: esc(t('Tên hàng')),
    k6: esc(t('SL')),
    k7: esc(t('Đơn giá')),
    k8: esc(t('Thành tiền')),
    k9: esc(t('Đã kiểm')),
    k10: esc(t('Tổng')),
    k11: esc(t('Ghi chú của shop')),
    k12: esc(t('Người đóng gói')),
    k13: esc(t('Người kiểm tra'))
  };
  const products = o.products ?? [];
  const total = products.reduce((s, p) => s + p.sellingPrice * p.qty, 0);
  return `<section class="page">
    <div class="phead">
      <div><h1>${L.k0}</h1><div class="muted">${esc(c.sender.companyName)} · ${L.k1}</div></div>
      <div class="pcode">${code128Svg(o.ref)}<div><strong>${esc(o.ref)}</strong></div></div>
    </div>
    <table class="meta"><tr>
      <td><span class="muted">${L.k2}</span><br><strong>${esc(o.receiver?.name || o.cnee)}</strong><br>${receiverLines(o.receiver, o.ct).map(esc).join('<br>')}${o.receiver?.phone ? `<br>${esc(o.receiver.phone)}` : ''}</td>
      <td><span class="muted">${L.k3}</span><br>${esc(o.createdAt)}<br><span class="muted">${L.k4}</span><br>${esc(c.sourceLabel(o.src))}${o.bill ? `<br><span class="muted">VA Bill</span><br>${esc(o.bill)}` : ''}</td>
    </tr></table>
    <table class="items">
      <thead><tr><th>#</th><th>${L.k5}</th><th>SKU</th><th class="num">${L.k6}</th><th class="num">${L.k7}</th><th class="num">${L.k8}</th><th>${L.k9}</th></tr></thead>
      <tbody>${products.map((p, i) => `<tr><td>${i + 1}</td><td>${esc(p.name)}</td><td>${esc(p.sku || '—')}</td><td class="num">${p.qty}</td><td class="num">${money(p.sellingPrice, o.currency, c.formatNumber)}</td><td class="num">${money(p.sellingPrice * p.qty, o.currency, c.formatNumber)}</td><td class="check">☐</td></tr>`).join('')}</tbody>
      <tfoot><tr><td colspan="3"><strong>${L.k10}</strong></td><td class="num"><strong>${o.items}</strong></td><td></td><td class="num"><strong>${money(total, o.currency, c.formatNumber)}</strong></td><td></td></tr></tfoot>
    </table>
    ${o.note ? `<p><span class="muted">${L.k11}:</span> ${esc(o.note)}</p>` : ''}
    <div class="signs"><div>${L.k12}</div><div>${L.k13}</div></div>
  </section>`;
}

const A4_CSS = `
  @page { size: A4; margin: 12mm; }
  .phead { display: flex; justify-content: space-between; align-items: flex-start; gap: 8mm; margin-bottom: 4mm; }
  .pcode { width: 70mm; text-align: center; }
  .pcode svg { width: 100%; height: 14mm; display: block; }
  .meta { margin-bottom: 4mm; } .meta td { width: 50%; }
  .items td.check { text-align: center; font-size: 13pt; }
  .signs { display: flex; justify-content: space-around; margin-top: 12mm; text-align: center; }
  .signs div { width: 60mm; padding-bottom: 22mm; border-bottom: 1px dotted #000; font-weight: bold; }
`;

/** Bảng kê giao hàng A4: tất cả đơn đã chọn trên 1 bảng + tổng + chữ ký bàn giao cho Việt An. */
function manifestPage(orders: ReadonlyArray<EcomOrder>, c: PrintContext): string {
  const { t } = c;
  /** Chữ đã dịch + escape — để template chỉ còn tên biến. */
  const L = {
    k0: esc(t('Bảng kê giao hàng')),
    k1: esc(t('Bên giao')),
    k2: esc(t('In lúc {time}', { time: c.printedAt })),
    k3: esc(t('STT')),
    k4: esc(t('Mã đơn')),
    k5: esc(t('Người nhận')),
    k6: esc(t('Nước đến')),
    k7: esc(t('Số SP')),
    k8: esc(t('Cân (kg)')),
    k9: esc(t('Giá trị')),
    k10: esc(t('Tổng {n} đơn', { n: orders.length })),
    k11: esc(t('Bên giao (khách hàng)')),
    k12: esc(t('Bên nhận (Việt An Express)'))
  };
  const items = orders.reduce((s, o) => s + o.items, 0);
  const kg = orders.reduce((s, o) => s + (o.kg || 0), 0);
  return `<section class="page">
    <h1>${L.k0}</h1>
    <div class="muted">${L.k1}: <strong>${esc(c.sender.companyName)}</strong> (${esc(c.sender.customerCode)})${c.sender.phone ? ` · ${esc(c.sender.phone)}` : ''} · ${L.k2}</div>
    <table class="items" style="margin-top:4mm">
      <thead><tr><th class="num">${L.k3}</th><th>${L.k4}</th><th>${L.k5}</th><th>${L.k6}</th><th class="num">${L.k7}</th><th class="num">${L.k8}</th><th class="num">${L.k9}</th><th>VA Bill</th></tr></thead>
      <tbody>${orders.map((o, i) => `<tr><td class="num">${i + 1}</td><td>${esc(o.ref)}</td><td>${esc(o.receiver?.name || o.cnee)}</td><td>${esc(o.receiver?.country ?? o.ct)}</td><td class="num">${o.items}</td><td class="num">${o.kg ? c.formatNumber(o.kg) : '—'}</td><td class="num">${money(o.value, o.currency, c.formatNumber)}</td><td>${esc(o.bill || '—')}</td></tr>`).join('')}</tbody>
      <tfoot><tr><td colspan="4"><strong>${L.k10}</strong></td><td class="num"><strong>${items}</strong></td><td class="num"><strong>${kg ? c.formatNumber(kg) : '—'}</strong></td><td colspan="2"></td></tr></tfoot>
    </table>
    <div class="signs"><div>${L.k11}</div><div>${L.k12}</div></div>
  </section>`;
}

/** Tài liệu HTML hoàn chỉnh để in (mở trong iframe rồi gọi print). */
export function buildPrintHtml(kind: PrintKind, orders: ReadonlyArray<EcomOrder>, c: PrintContext): string {
  const body =
    kind === 'label' ? orders.map(o => labelPage(o, c)).join('')
    : kind === 'packing' ? orders.map(o => packingPage(o, c)).join('')
    : manifestPage(orders, c);
  const css = BASE_CSS + (kind === 'label' ? LABEL_CSS : A4_CSS);
  const title = kind === 'manifest' ? 'manifest' : kind === 'label' ? 'labels' : 'packing-list';
  return `<!doctype html><html><head><meta charset="utf-8"><title>${title}</title><style>${css}</style></head><body>${body}</body></html>`;
}
