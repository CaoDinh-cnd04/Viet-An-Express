import { describe, expect, it } from 'vitest';
import { VOLUME_DIVISOR } from '@/shared/lib/shipping-weight';
import { defaultValues, emptyPackage } from '../schema';
import { buildDraftPayload, summarizePackages } from './shipment';

const packageRow = (weight: string, length = '20', width = '30', height = '40', qty = '1') => ({ ...emptyPackage(), qty, weight, length, width, height });
describe('cân tính cước của form theo hãng', () => {
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
