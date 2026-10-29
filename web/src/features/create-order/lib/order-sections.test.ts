import { describe, expect, it } from 'vitest';
import { secId, stepSections } from './order-sections';

describe('stepSections', () => {
  it('hàng hóa: 3 bước, có bảng kiện và Invoice', () => {
    const steps = stepSections(true);
    expect(steps).toHaveLength(3);
    expect(steps[1]!.map(s => s.id)).toEqual([secId('packages'), secId('addons')]);
    expect(steps[2]!.map(s => s.id)).toEqual([secId('invoice')]);
  });

  it('chứng từ: 2 bước, không có bảng kiện / Invoice', () => {
    const steps = stepSections(false);
    expect(steps).toHaveLength(2);
    expect(steps.flat().map(s => s.id)).not.toContain(secId('packages'));
    expect(steps.flat().map(s => s.id)).not.toContain(secId('invoice'));
  });
});
