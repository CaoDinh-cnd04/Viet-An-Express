import { describe, expect, it } from 'vitest';
import { addressSize } from './layout';

describe('addressSize', () => {
  it('địa chỉ càng dài chữ càng nhỏ', () => {
    expect(addressSize('262/12 Đ. Lê Văn Sỹ, Phường 14, Quận 3, HCM')).toBe('normal');
    expect(addressSize('Số 18A Ngõ 68 Đường Mễ Trì Hạ, Phường Mễ Trì, Quận Nam Từ Liêm, Hà Nội')).toBe('long');
    expect(addressSize('Số 18A Ngõ 68 Đường Mễ Trì Hạ, Phường Mễ Trì, Quận Nam Từ Liêm, Thành phố Hà Nội, Việt Nam, tầng 3 tòa B')).toBe('veryLong');
  });
});
