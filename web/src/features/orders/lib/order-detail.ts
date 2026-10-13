import type { OrderInvoiceItem, OrderPackage, OrderReceiver } from '../types';
import { packageWeights, shipmentWeight, weightRule, type WeightService } from '@/shared/lib/shipping-weight';

/** Cân quy đổi = D×R×C / 5000 (cm → kg) — cùng quy tắc với tạo đơn. */
export const VOLUME_DIVISOR = 5000;

const round2 = (n: number) => Math.round(n * 100) / 100;

export interface PackageSummary {
  pieces: number;
  /** Tổng cân thực (kg). */
  gross: number;
  /** Tổng cân quy đổi (kg). */
  volume: number;
  /** Tổng cân tính cước từng kiện, làm tròn theo hãng/dịch vụ. */
  chargeable: number;
}

/** Cân quy đổi của cả dòng kiện (SL × D×R×C / 5000). */
export function lineVolume(p: OrderPackage, service: WeightService = ''): number {
  return p.volumetricWeight ?? round2(packageWeights(p, weightRule(service)).volume);
}

export function summarizePackages(list: readonly OrderPackage[], service: WeightService = ''): PackageSummary {
  const pieces = list.reduce((s, p) => s + p.qty, 0);
  const gross = round2(list.reduce((s, p) => s + p.qty * p.weightKg, 0));
  const rule = weightRule(service);
  const volume = round2(list.reduce((s, p) => s + lineVolume(p, service), 0));
  const chargeable = shipmentWeight(list.reduce((s, p) => s + (p.chargeableWeight ?? packageWeights(p, rule).chargeable), 0), rule);
  return { pieces, gross, volume, chargeable };
}

export function invoiceTotal(items: readonly OrderInvoiceItem[]): number {
  return round2(items.reduce((s, i) => s + (i.amount || i.qty * i.price), 0));
}

/** Địa chỉ người nhận thành các dòng hiển thị, bỏ dòng trống. */
export function receiverAddress(r: OrderReceiver): string[] {
  const cityLine = [r.city, r.state, r.postal].map(s => s.trim()).filter(Boolean).join(', ');
  return [r.addr1, r.addr2, r.addr3, cityLine, r.country].map(s => s.trim()).filter(Boolean);
}

/** Bỏ các cặp [nhãn, giá trị] có giá trị trống / "—" để khung chi tiết gọn. */
export function filledRows<T extends readonly [string, string | null | undefined]>(rows: readonly T[]): Array<[string, string]> {
  return rows.filter(([, v]) => !!v && v.trim() !== '' && v.trim() !== '—').map(([k, v]) => [k, v!.trim()]);
}
