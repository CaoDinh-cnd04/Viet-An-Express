import { describe, expect, it } from 'vitest';
import { normalizeUnit, OTHER_UNIT, qtyError, QTY_INTEGER_MESSAGE, QTY_POSITIVE_MESSAGE, unitChoice, UNIT_MAX } from './units';

describe('unitChoice', () => {
  it('đơn vị có sẵn (không phân biệt hoa thường)', () => {
    expect(unitChoice('PCS')).toBe('PCS');
    expect(unitChoice('bag')).toBe('Bag');
  });
  it('đơn vị tự nhập hoặc đang trống → Khác', () => {
    expect(unitChoice('KG')).toBe(OTHER_UNIT);
    expect(unitChoice('')).toBe(OTHER_UNIT);
  });
});

describe('normalizeUnit', () => {
  it('trống → PCS', () => expect(normalizeUnit('  ')).toBe('PCS'));
  it('viết đúng chuẩn đơn vị có sẵn', () => expect(normalizeUnit(' box ')).toBe('BOX'));
  it('giữ đơn vị khách tự nhập', () => expect(normalizeUnit('Đôi')).toBe('Đôi'));
  it('cắt bớt nếu quá dài', () => expect(normalizeUnit('x'.repeat(30))).toHaveLength(UNIT_MAX));
});

describe('qtyError — số lượng theo ĐVT', () => {
  it('ĐVT có sẵn: chỉ số nguyên > 0', () => {
    expect(qtyError('3', 'PCS')).toBeUndefined();
    expect(qtyError('1.5', 'PCS')).toBe(QTY_INTEGER_MESSAGE);
    expect(qtyError('2.0', 'Bag')).toBe(QTY_INTEGER_MESSAGE);
    expect(qtyError('0', 'BOX')).toBe(QTY_INTEGER_MESSAGE);
    expect(qtyError('', 'SET')).toBe(QTY_INTEGER_MESSAGE);
  });

  it('ĐVT khách tự nhập: số thực > 0', () => {
    expect(qtyError('1.5', 'KG')).toBeUndefined();
    expect(qtyError('0', 'KG')).toBe(QTY_POSITIVE_MESSAGE);
    expect(qtyError('abc', 'M')).toBe(QTY_POSITIVE_MESSAGE);
  });
});
