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
    const p = { qty: 2, length: 20.1, width: 20.5, height: 20.9, weightKg: 1 };
    expect(packageWeights(p, weightRule('UPS')).volume).toBe(2 * 20 * 21 * 21 / 5000);
    expect(packageWeights(p, weightRule('Fedex')).volume).toBe(2 * 21 * 21 * 21 / 5000);
    expect(packageWeights(p, weightRule('DHL')).volume).toBeCloseTo(2 * 20.1 * 20.5 * 20.9 / 5000);
  });
});
