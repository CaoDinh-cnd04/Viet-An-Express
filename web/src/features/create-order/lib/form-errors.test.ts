import { describe, expect, it } from 'vitest';
import type { FieldErrors } from 'react-hook-form';
import type { CreateOrderValues } from '../schema';
import { fieldLabel, listFormErrors } from './form-errors';

describe('listFormErrors', () => {
  it('trải lỗi lồng nhau, ghi tên ô dễ hiểu', () => {
    const errors = {
      shipper: { email: { type: 'custom', message: 'Email không hợp lệ' } },
      packages: { 0: { weight: { type: 'custom', message: 'Nhập cân/kiện' } }, root: { type: 'custom', message: 'Tổng SL chưa khớp' } }
    } as unknown as FieldErrors<CreateOrderValues>;

    expect(listFormErrors(errors)).toEqual([
      { path: 'shipper.email', label: 'Email người gửi', message: 'Email không hợp lệ' },
      { path: 'packages.0.weight', label: 'Kiện 1: cân/kiện', message: 'Nhập cân/kiện' },
      { path: 'packages', label: 'Chi tiết kiện hàng', message: 'Tổng SL chưa khớp' }
    ]);
  });

  it('không lỗi → danh sách rỗng', () => {
    expect(listFormErrors({})).toEqual([]);
  });
});

describe('fieldLabel', () => {
  it('dòng mặt hàng invoice', () => {
    expect(fieldLabel('invoice.items.2.price')).toBe('Mặt hàng 3: đơn giá');
  });
});
