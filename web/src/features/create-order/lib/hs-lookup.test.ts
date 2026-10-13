import { describe, expect, it } from 'vitest';
import { formatHs, hs6Of } from './hs-lookup';

describe('formatHs', () => {
  it('nhóm 4 + 2 số đầu, phần quốc gia nhóm 2 số', () => {
    expect(formatHs('080450')).toBe('0804.50');
    expect(formatHs('0804500010')).toBe('0804.50.00.10');
    expect(formatHs('0804.50.0010')).toBe('0804.50.00.10');
    expect(formatHs('08045000')).toBe('0804.50.00');
  });

  it('mã quá ngắn giữ nguyên', () => {
    expect(formatHs('08')).toBe('08');
    expect(formatHs('')).toBe('');
  });
});

describe('hs6Of', () => {
  it('lấy 6 số quốc tế', () => {
    expect(hs6Of('0804.50.00.10')).toBe('0804.50');
    expect(hs6Of('0804')).toBe('');
  });
});
