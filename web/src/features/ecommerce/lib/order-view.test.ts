import { describe, expect, it } from 'vitest';
import type { EcomOrder } from '../types';
import { countByView, displayStatus, filterByView, formatMoney, receiverLines } from './order-view';

const order = (id: string, st: EcomOrder['st'], bill = '', extra: Partial<EcomOrder> = {}): EcomOrder =>
  ({ id, src: 'tiktok', ref: id, bill, cnee: 'A', ct: 'US', items: 1, kg: 1, st, createdAt: '05/10/2026 09:00', ...extra });

const inbox = [order('1', 'created', '', { issues: ['Thiếu số điện thoại người nhận'] }), order('2', 'created')];
const mine = [order('3', 'created', '', { confirmed: true }), order('4', 'exception', '', { confirmed: true }), order('5', 'delivered', 'VA2', { confirmed: true })];

describe('filterByView / countByView', () => {
  it('tab Đơn hàng: cần bổ sung / sẵn sàng gửi', () => {
    expect(filterByView(inbox, 'needsInfo').map(o => o.id)).toEqual(['1']);
    expect(filterByView(inbox, 'ready').map(o => o.id)).toEqual(['2']);
    expect(countByView(inbox, 'inbox')).toEqual({ all: 2, needsInfo: 1, ready: 1 });
  });

  it('trang Đơn hàng E-com: chờ tạo bill / đã có bill / lỗi', () => {
    expect(filterByView(mine, 'waiting').map(o => o.id)).toEqual(['3']);
    expect(filterByView(mine, 'billed').map(o => o.id)).toEqual(['5']);
    expect(filterByView(mine, 'exception').map(o => o.id)).toEqual(['4']);
    expect(countByView([], 'mine')).toEqual({ all: 0, waiting: 0, billed: 0, exception: 0 });
  });
});

describe('displayStatus', () => {
  it('đơn mới về: cần bổ sung hoặc sẵn sàng gửi', () => {
    expect(displayStatus({ st: 'created', bill: '', issues: ['Chưa có sản phẩm'] }).label).toBe('Cần bổ sung');
    expect(displayStatus({ st: 'created', bill: '' }).label).toBe('Sẵn sàng gửi');
  });

  it('đơn đã xác nhận / có bill / lỗi', () => {
    expect(displayStatus({ st: 'created', bill: '', confirmed: true }).label).toBe('Đã xác nhận gửi');
    expect(displayStatus({ st: 'created', bill: 'VA1', confirmed: true }).label).toBe('Đã tạo');
    expect(displayStatus({ st: 'exception', bill: '' }).tone).toBe('danger');
  });
});

describe('receiverLines / formatMoney', () => {
  it('ghép địa chỉ, bỏ dòng trống', () => {
    expect(receiverLines({ address1: '1 Main St', address2: '', city: 'Sydney', state: 'NSW', postal: '2000', country: 'Australia' }, 'AU'))
      .toEqual(['1 Main St', 'Sydney, NSW, 2000', 'Australia']);
    expect(receiverLines(undefined, 'Japan')).toEqual(['Japan']);
  });

  it('giá trị kèm tiền tệ', () => {
    const f = (n: number) => String(n);
    expect(formatMoney(59.9, 'USD', f)).toBe('59.9 USD');
    expect(formatMoney(10, null, f)).toBe('10');
    expect(formatMoney(null, 'USD', f)).toBe('');
  });
});
