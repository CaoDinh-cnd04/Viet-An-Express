import { describe, expect, it } from 'vitest';
import { safeLink } from './link';

describe('safeLink', () => {
  it('nhận link http / https, cắt khoảng trắng', () => {
    expect(safeLink(' https://vietanexpress.com.vn/bang-gia?x=1 ')).toBe('https://vietanexpress.com.vn/bang-gia?x=1');
    expect(safeLink('http://x.vn')).toBe('http://x.vn');
  });

  it('chặn link không phải web hoặc trống', () => {
    expect(safeLink('javascript:alert(1)')).toBeNull();
    expect(safeLink('data:text/html,abc')).toBeNull();
    expect(safeLink('vietanexpress.com.vn')).toBeNull();
    expect(safeLink('ftp://x.vn')).toBeNull();
    expect(safeLink('')).toBeNull();
    expect(safeLink(null)).toBeNull();
    expect(safeLink(`https://x.vn/${'a'.repeat(500)}`)).toBeNull();
  });
});
