import { describe, expect, it } from 'vitest';
import type { EcomOrder } from '../types';
import { fromEditForm, isLatin, toEditForm } from './order-edit';

const order: EcomOrder = {
  id: '7', src: 'tiktok', ref: '#1002', bill: '', cnee: '山田 太郎', ct: 'Japan', items: 2, kg: 0, st: 'created', createdAt: '11/04/2025 08:21',
  receiver: { name: '山田 太郎', phone: '+818000000000', address1: '中央三丁目3-1', city: '京田辺市', state: 'Kyoto', postal: '610-0313', countryCode: 'JP', country: 'Japan' },
  products: [{ name: 'T SHIRT - S', sku: '', qty: 2, fobPrice: 30, sellingPrice: 30 }],
  hub: 'SGN'
};

describe('order-edit', () => {
  it('đơn → form → body giữ đúng dữ liệu', () => {
    const f = toEditForm(order);
    expect(f.kg).toBe('');
    expect(f.products[0]).toEqual({ name: 'T SHIRT - S', sku: '', qty: '2', price: '30', hsCode: '' });
    const body = fromEditForm({ ...f, name: ' Taro Yamada ', kg: '0.45', products: [{ ...f.products[0]!, hsCode: '6109.10' }] }, order);
    expect(body.receiver.name).toBe('Taro Yamada');
    expect(body.receiver.countryCode).toBe('JP');
    expect(body.kg).toBe(0.45);
    expect(body.products[0]).toEqual({ name: 'T SHIRT - S', sku: '', qty: 2, fobPrice: 30, sellingPrice: 30, hsCode: '6109.10' });
    expect(body.hub).toBe('SGN');
  });

  it('cân trống hoặc 0 → null', () => {
    expect(fromEditForm({ ...toEditForm(order), kg: '0' }, order).kg).toBeNull();
  });

  it('isLatin', () => {
    expect(isLatin('ØSAMA Café 13 Winsor Place')).toBe(true);
    expect(isLatin('中央三丁目3-1')).toBe(false);
  });
});
