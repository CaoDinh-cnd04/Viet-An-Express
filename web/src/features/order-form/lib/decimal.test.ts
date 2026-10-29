import { describe, expect, it } from 'vitest';
import { hasMaxDecimals } from './decimal';

describe('hasMaxDecimals', () => {
  it('kiểm tra giá trị có sẵn (nháp cũ, dán vào)', () => {
    expect(hasMaxDecimals('40', 2)).toBe(true);
    expect(hasMaxDecimals('40.5', 2)).toBe(true);
    expect(hasMaxDecimals('40.55', 2)).toBe(true);
    expect(hasMaxDecimals('1.0000000000000000000003', 2)).toBe(false);
    expect(hasMaxDecimals('', 2)).toBe(false);
    expect(hasMaxDecimals('abc', 2)).toBe(false);
  });
});
