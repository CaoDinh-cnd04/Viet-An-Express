import { describe, expect, it } from 'vitest';
import { packageWeights, shipmentWeight, weightRule, VOLUME_DIVISOR } from './shipping-weight';
import { summarizePackages, buildDraftPayload } from '@/features/create-order/lib/shipment';
import { defaultValues, emptyPackage } from '@/features/create-order/schema';

const packageRow = (weight: string, length = '20', width = '30', height = '40', qty = '1') => ({ ...emptyPackage(), qty, weight, length, width, height });
describe('carrier billable weights', () => {
  it.each(['', 'DHL', 'Fedex', 'UPS', 'Aramex', 'SEA', 'Chuyên tuyến'])('%s uses divisor 5000 and bills each piece independently', carrier => {
    expect(VOLUME_DIVISOR).toBe(5000);
    const s = summarizePackages([packageRow('6'), packageRow('2', '30', '50', '10')], { carrier });
    expect(s).toEqual({ pieces: 2, grossWeight: 8, volumetricWeight: 7.8, chargeableWeight: 9 });
  });
  it.each([
    ['UPS - VN Saver', '1.1', '3', 4.5],
    ['UPS|VN Expedited', '1.1', '3', 6],
    ['DHL - VN', '1.1', '3', 4.5],
    ['Fedex - VN', '1.1', '3', 3.5],
    ['Chuyên tuyến - Singapore', '1.1', '3', 3.3],
    ['UPS - VN Saver', '1.5001', '1', 2],
    ['UPS - VN Saver', '1.5', '1', 1.5]
  ])('%s rounds at the correct stage (%s kg x %s)', (service, weight, qty, expected) => {
    expect(summarizePackages([packageRow(weight, '', '', '', qty)], service).chargeableWeight).toBe(expected);
  });
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
  it('does not lose small per-piece volumes before quantity and sum', () => {
    const s = summarizePackages([packageRow('0', '1', '1', '1', '100')]);
    expect(s.volumetricWeight).toBe(0.02);
    expect(s.chargeableWeight).toBe(0.02);
    expect(summarizePackages([packageRow('6', '20', '30', '40', '0')]).chargeableWeight).toBe(0);
  });
  it('the draft label uses the selected hub rule', () => {
    const v = defaultValues();
    v.service = { ...v.service, carrier: 'UPS', hub: 'UPS - VN Expedited' };
    v.packages = [packageRow('1.1', '', '', '', '3')];
    expect(buildDraftPayload(v, 'draft').pcs).toBe('3 kiện · 6 kg');
  });
});
