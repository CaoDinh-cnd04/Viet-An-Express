import { describe, expect, it } from 'vitest';
import { packageWeights, shipmentWeight, weightRule } from './shipping-weight';

describe('carrier billable weights', () => {
  it.each([
    ['DHL', 30, 30], ['DHL', 30.1, 31], ['DHL', 31.5, 32],
    ['Fedex - VN', 20.1, 20.5], ['Fedex - VN', 20.51, 21],
    ['Fedex - SIN 3 (FICP)', 31.1, 31.5], ['Fedex - SIN 3 (FICP)', 32.51, 33]
  ])('%s total %s -> %s', (service, total, expected) => {
    expect(shipmentWeight(total, weightRule(service))).toBe(expected);
  });
  it('rounds dimensions by carrier, including halfway values', () => {
    // Quy đổi cả dòng làm tròn lên 0,5 kg.
    const p = { qty: 10, length: 20.1, width: 20.5, height: 20.9, weightKg: 1 };
    expect(packageWeights(p, weightRule('UPS')).volume).toBe(18); // 10 × 20 × 21 × 21 / 5000 = 17,64
    expect(packageWeights(p, weightRule('Fedex')).volume).toBe(19); // 10 × 21³ / 5000 = 18,52
    expect(packageWeights(p, weightRule('DHL')).volume).toBe(17.5); // 10 × 20,1 × 20,5 × 20,9 / 5000 = 17,22
  });
});
