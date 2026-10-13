/** Kilograms / centimetres. Keep in sync with backend Domain/ShippingRules.cs. */
export const VOLUME_DIVISOR = 5000;
export type WeightService = string | { carrier?: string; hub?: string };
export interface WeightRule {
  dimension: 'none' | 'nearest' | 'up';
  pieceStep: number;
  shipmentStep: number;
  threshold?: number;
  aboveStep?: number;
  label: string;
}
export function weightRule(service: WeightService = ''): WeightRule {
  const name = (typeof service === 'string' ? service : `${service.carrier ?? ''} ${service.hub ?? ''}`).trim().toLowerCase();
  if (/^ups\b/.test(name)) return { dimension: 'nearest', pieceStep: name.includes('expedited') ? 1 : 0.5, shipmentStep: 0, label: name.includes('expedited') ? 'UPS Expedited: từng kiện làm tròn lên 1 kg' : 'UPS Express: từng kiện làm tròn lên 0,5 kg' };
  if (/^dhl\b/.test(name)) return { dimension: 'none', pieceStep: 0.5, shipmentStep: 0.5, threshold: 30, aboveStep: 1, label: 'DHL: từng kiện lên 0,5 kg; tổng trên 30 kg lên 1 kg' };
  if (/^fedex\b/.test(name)) return { dimension: 'up', pieceStep: 0, shipmentStep: 0.5, threshold: name.includes('ficp') ? 32.5 : 20.5, aboveStep: 1, label: name.includes('ficp') ? 'FedEx FICP: tổng đến 32,5 kg lên 0,5 kg; trên mức này lên 1 kg' : 'FedEx: tổng đến 20,5 kg lên 0,5 kg; trên mức này lên 1 kg' };
  return { dimension: 'none', pieceStep: 0, shipmentStep: 0, label: 'Cân tính cước = tổng cân lớn hơn của từng kiện' };
}
export const roundWeight = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
const positive = (n: number) => Number.isFinite(n) && n > 0 ? n : 0;
export function roundUpWeight(kg: number, step: number): number {
  // Do not round to 2 decimals before ceiling (e.g. 1.5001 must become 2, not 1.5).
  return step > 0 ? Math.ceil(positive(kg) / step - 1e-10) * step : positive(kg);
}
export function weightDimension(cm: number, rule: WeightRule): number {
  cm = positive(cm);
  return rule.dimension === 'up' ? Math.ceil(cm) : rule.dimension === 'nearest' ? Math.floor(cm + 0.5) : cm;
}
export interface WeightPiece { qty: number; length: number; width: number; height: number; weightKg: number }
export function packageWeights(p: WeightPiece, rule: WeightRule) {
  const qty = Math.max(0, Math.trunc(positive(p.qty)));
  const volumePerPiece = weightDimension(p.length, rule) * weightDimension(p.width, rule) * weightDimension(p.height, rule) / VOLUME_DIVISOR;
  return { gross: positive(p.weightKg) * qty, volume: volumePerPiece * qty,
    chargeable: roundUpWeight(Math.max(positive(p.weightKg), volumePerPiece), rule.pieceStep) * qty };
}
export function shipmentWeight(kg: number, rule: WeightRule): number {
  const step = rule.threshold !== undefined && kg > rule.threshold ? rule.aboveStep ?? rule.shipmentStep : rule.shipmentStep;
  return roundWeight(roundUpWeight(kg, step));
}
