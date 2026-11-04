import { describe, expect, it } from 'vitest';
import { hasDiacritics, stripDiacritics } from './text';

describe('stripDiacritics', () => {
  it('bỏ dấu tiếng Việt, giữ chữ hoa / thường', () => {
    expect(stripDiacritics('Đức Anh Việt Nam')).toBe('Duc Anh Viet Nam');
    expect(stripDiacritics('đường Trường Chinh')).toBe('duong Truong Chinh');
    expect(stripDiacritics('ABC 123')).toBe('ABC 123');
  });
});

describe('hasDiacritics', () => {
  it('nhận chữ tiếng Việt có dấu, chữ tiếng Anh thì không', () => {
    expect(hasDiacritics('xoài sấy dẻo')).toBe(true);
    expect(hasDiacritics('Đồ chơi')).toBe(true);
    expect(hasDiacritics('Soft dried mango, Cotton T-shirt')).toBe(false);
  });
});
