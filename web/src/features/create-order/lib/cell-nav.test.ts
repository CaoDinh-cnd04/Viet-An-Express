import { describe, expect, it } from 'vitest';
import { arrowMove, hasMaxDecimals, sanitizeDecimal } from './cell-nav';

describe('arrowMove', () => {
  it('cuối ô + → thì sang ô sau, đầu ô + ← thì về ô trước', () => {
    expect(arrowMove('ArrowRight', 2, 2, 2)).toBe('next');
    expect(arrowMove('ArrowLeft', 0, 0, 2)).toBe('prev');
    expect(arrowMove('ArrowRight', 0, 0, 0)).toBe('next'); // ô trống
  });

  it('con trỏ ở giữa / bôi đen / ô không đọc được vị trí → không chuyển', () => {
    expect(arrowMove('ArrowRight', 1, 1, 2)).toBeNull();
    expect(arrowMove('ArrowLeft', 1, 1, 2)).toBeNull();
    expect(arrowMove('ArrowRight', 0, 2, 2)).toBeNull();
    expect(arrowMove('ArrowRight', null, null, 2)).toBeNull();
    expect(arrowMove('ArrowUp', 2, 2, 2)).toBeNull();
  });
});

describe('sanitizeDecimal', () => {
  it('giữ chữ số và 1 dấu thập phân', () => {
    expect(sanitizeDecimal('12,5kg')).toBe('12.5');
    expect(sanitizeDecimal('1.2.3')).toBe('1.23');
    expect(sanitizeDecimal('a45')).toBe('45');
  });

  it('số nguyên chỉ giữ chữ số', () => {
    expect(sanitizeDecimal('3.5', true)).toBe('35');
  });
});

describe('giới hạn số lẻ (đơn giá invoice tối đa 2 số lẻ)', () => {
  it('cắt bớt số lẻ khi gõ', () => {
    expect(sanitizeDecimal('1.0000000000000000000003', false, 2)).toBe('1.00');
    expect(sanitizeDecimal('12,345', false, 2)).toBe('12.34');
    expect(sanitizeDecimal('40.', false, 2)).toBe('40.');
    expect(sanitizeDecimal('7', false, 2)).toBe('7');
  });

  it('kiểm tra giá trị có sẵn (nháp cũ, dán vào)', () => {
    expect(hasMaxDecimals('40', 2)).toBe(true);
    expect(hasMaxDecimals('40.5', 2)).toBe(true);
    expect(hasMaxDecimals('40.55', 2)).toBe(true);
    expect(hasMaxDecimals('1.0000000000000000000003', 2)).toBe(false);
    expect(hasMaxDecimals('', 2)).toBe(false);
    expect(hasMaxDecimals('abc', 2)).toBe(false);
  });
});
