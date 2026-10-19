import { describe, expect, it } from 'vitest';
import { formatHs, groupByHs6, hs6Of } from './hs-lookup';

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

describe('groupByHs6', () => {
  const tee = (code: string, path: string[]) => ({ hs6: '6109.10', nationalCode: code, path });
  it('gom theo HS6, đưa đường dẫn chung lên tiêu đề nhóm', () => {
    const groups = groupByHs6([
      tee('6109.10.00.12', ['T-shirts…', 'Of cotton', "Men's or boys'", 'Other T-shirts']),
      { hs6: '080450', nationalCode: '0804.50.40.40', path: [] },
      tee('6109.10.00.04', ['T-shirts…', 'Of cotton', "Men's or boys'"])
    ]);
    expect(groups.map(g => g.hs6)).toEqual(['6109.10', '0804.50']);
    expect(groups[0]!.path).toEqual(['T-shirts…', 'Of cotton', "Men's or boys'"]);
    expect(groups[0]!.lines.map(l => l.rest)).toEqual([['Other T-shirts'], []]);
  });

  it('nhóm 1 dòng: cả đường dẫn lên tiêu đề; không có đường dẫn thì rỗng', () => {
    expect(groupByHs6([tee('6109.10.00.12', ['A', 'B'])])[0]!.path).toEqual(['A', 'B']);
    expect(groupByHs6([{ hs6: '0804.50' }])[0]).toMatchObject({ path: [], lines: [{ rest: [] }] });
  });
});
