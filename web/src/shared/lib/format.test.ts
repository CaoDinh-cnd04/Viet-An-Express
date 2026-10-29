import { describe, expect, it } from 'vitest';
import { formatMoney, formatViDate, relativeDayLabel, todayIso } from './format';

describe('formatMoney', () => {
  it('giá trị kèm tiền tệ, trống khi chưa có giá trị', () => {
    expect(formatMoney(1500, 'USD')).toBe('1.500 USD');
    expect(formatMoney(59.9, 'USD')).toBe('59,9 USD');
    expect(formatMoney(10, null)).toBe('10');
    expect(formatMoney(null, 'USD')).toBe('');
  });
});

describe('ngày', () => {
  const today = new Date(2026, 9, 28, 9, 30);

  it('formatViDate → dd/mm/yyyy', () => {
    expect(formatViDate(new Date(2026, 0, 5))).toBe('05/01/2026');
  });

  it('relativeDayLabel: hôm nay / hôm qua / giữ nguyên', () => {
    expect(relativeDayLabel('28/10/2026', today)).toBe('Hôm nay');
    expect(relativeDayLabel('27/10/2026', today)).toBe('Hôm qua');
    expect(relativeDayLabel('01/11/2026', new Date(2026, 10, 2))).toBe('Hôm qua');
    expect(relativeDayLabel('20/10/2026', today)).toBe('20/10/2026');
  });

  it('todayIso → yyyy-mm-dd', () => {
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
