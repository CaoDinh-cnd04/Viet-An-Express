import { describe, expect, it } from 'vitest';
import type { EcomOrder } from '../types';
import { buildPrintHtml, type PrintContext } from './print-docs';

const ctx: PrintContext = {
  sender: { companyName: 'Shop A', customerCode: 'PTA1', phone: '0909' },
  printedAt: '05/10/2026 10:00',
  t: (text, vars) => Object.entries(vars ?? {}).reduce((s, [k, v]) => s.replace(`{${k}}`, String(v)), text),
  formatNumber: n => String(n),
  sourceLabel: s => (s === 'tiktok' ? 'TikTok Shop' : s)
};

const order = (id: string, extra: Partial<EcomOrder> = {}): EcomOrder => ({
  id, src: 'tiktok', ref: `#c${id}`, bill: '', cnee: 'Jane', ct: 'United States', items: 2, kg: 0, st: 'created', createdAt: '11/04/2025 07:02',
  value: 115, currency: 'USD',
  receiver: { name: 'Jane <b>Roe</b>', phone: '+19175550100', address1: '1 Main St', city: 'Glen Ridge', state: 'NJ', postal: '07028', countryCode: 'US', country: 'United States' },
  products: [{ name: 'Tee', sku: 'T1', qty: 2, fobPrice: 45, sellingPrice: 45 }],
  ...extra
});

describe('buildPrintHtml', () => {
  it('nhãn A6: mỗi đơn 1 trang, có mã vạch, người nhận, người gửi; escape HTML', () => {
    const html = buildPrintHtml('label', [order('1'), order('2')], ctx);
    expect(html).toContain('size: 105mm 148mm');
    expect(html.match(/class="page label"/g)).toHaveLength(2);
    expect(html).toContain('<svg');
    expect(html).toContain('Jane &lt;b&gt;Roe&lt;/b&gt;');
    expect(html).not.toContain('<b>Roe</b>');
    expect(html).toContain('Shop A · PTA1');
    expect(html).toContain('Glen Ridge, NJ, 07028');
    expect(html).toContain('<span>TikTok Shop · 11/04/2025 07:02</span>');
  });

  it('phiếu đóng gói: bảng sản phẩm và tổng tiền', () => {
    const html = buildPrintHtml('packing', [order('1')], ctx);
    expect(html).toContain('size: A4');
    expect(html).toContain('Phiếu đóng gói');
    expect(html).toContain('90 USD');
  });

  it('bảng kê: 1 trang cho mọi đơn, tổng số đơn / SP / cân', () => {
    const html = buildPrintHtml('manifest', [order('1', { kg: 0.5 }), order('2', { kg: 1.25 })], ctx);
    expect(html.match(/class="page"/g)).toHaveLength(1);
    expect(html).toContain('Tổng 2 đơn');
    expect(html).toContain('<strong>4</strong>');
    expect(html).toContain('<strong>1.75</strong>');
  });
});
