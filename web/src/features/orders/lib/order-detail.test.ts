import { describe, expect, it } from 'vitest';
import { filledRows, invoiceTotal, lineVolume, receiverAddress, summarizePackages } from './order-detail';

const pkg = (qty: number, l: number, w: number, h: number, kg: number) => ({ qty, packType: 'CARTON', length: l, width: w, height: h, weightKg: kg });

describe('kiện hàng', () => {
  it('giữ số quy đổi/cân tính cước đã lưu khi kích thước trong DB đã làm tròn', () => {
    const p = { ...pkg(2, 21, 21, 21, 1), volumetricWeight: 3.45, chargeableWeight: 4 };
    expect(lineVolume(p, 'UPS')).toBe(3.45);
    expect(summarizePackages([p], 'UPS').chargeable).toBe(4);
  });
  it('cân quy đổi cả dòng', () => expect(lineVolume(pkg(2, 50, 40, 30, 5))).toBe(24));
  it('tổng: cân tính cước = max(thực, quy đổi)', () => {
    expect(summarizePackages([pkg(2, 50, 40, 30, 5), pkg(1, 10, 10, 10, 3)])).toEqual({ pieces: 3, gross: 13, volume: 24.2, chargeable: 27 });
    expect(summarizePackages([])).toEqual({ pieces: 0, gross: 0, volume: 0, chargeable: 0 });
  });
});

describe('invoice', () => {
  it('tổng tiền, thiếu thành tiền thì lấy SL × giá', () => {
    const item = { descEn: '', descVi: '', unit: 'PCS', hs: '', origin: 'VN' };
    expect(invoiceTotal([{ ...item, qty: 5, price: 8, amount: 40 }, { ...item, qty: 2, price: 1.25, amount: 0 }])).toBe(42.5);
  });
});

describe('hiển thị', () => {
  it('địa chỉ người nhận bỏ dòng trống', () => {
    const r = { company: '', contact: '', tel: '', email: '', taxId: '', country: 'United Kingdom', city: 'LONDON', state: '', postal: 'EC1A', addr1: '1 King St', addr2: ' ', addr3: '' };
    expect(receiverAddress(r)).toEqual(['1 King St', 'LONDON, EC1A', 'United Kingdom']);
  });
  it('bỏ dòng trống / "—"', () => {
    expect(filledRows([['A', 'x'], ['B', ''], ['C', '—'], ['D', null]] as const)).toEqual([['A', 'x']]);
  });
});
