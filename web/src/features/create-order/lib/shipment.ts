import { fill } from '@/shared/i18n';
import { MULTI_CATEGORY, RULES } from '../constants';
import type { CreateOrderValues, InvoiceItemValues, PackageValues } from '../schema';
import { packageWeights, roundWeight, shipmentWeight, weightRule, type WeightService } from '@/shared/lib/shipping-weight';

/** Chuỗi ô nhập → số; rỗng / sai → 0. */
export const toNumber = (value: string | undefined): number => {
  const n = Number(value);
  return Number.isFinite(n) ? n : 0;
};

const round = (n: number, digits = 2) => Math.round(n * 10 ** digits) / 10 ** digits;

/** Trọng lượng quy đổi của một dòng kiện = D×R×C / 5000 × SL. */
export function volumetricWeight(p: Pick<PackageValues, 'qty' | 'length' | 'width' | 'height'>, service: WeightService = ''): number {
  return roundWeight(packageWeights({ qty: toNumber(p.qty), length: toNumber(p.length), width: toNumber(p.width), height: toNumber(p.height), weightKg: 0 }, weightRule(service)).volume);
}

export function packageChargeableWeight(p: PackageValues, service: WeightService = ''): number {
  return roundWeight(packageWeights({ qty: toNumber(p.qty), length: toNumber(p.length), width: toNumber(p.width), height: toNumber(p.height), weightKg: toNumber(p.weight) }, weightRule(service)).chargeable);
}

export interface PackageSummary {
  pieces: number;
  grossWeight: number;
  volumetricWeight: number;
  /** Tổng max(cân thực, quy đổi) từng kiện, làm tròn theo dịch vụ. */
  chargeableWeight: number;
}

export function summarizePackages(packages: ReadonlyArray<PackageValues>, service: WeightService = ''): PackageSummary {
  const rule = weightRule(service);
  const weights = packages.map(p => packageWeights({ qty: toNumber(p.qty), length: toNumber(p.length), width: toNumber(p.width), height: toNumber(p.height), weightKg: toNumber(p.weight) }, rule));
  return { pieces: packages.reduce((s, p) => s + Math.max(0, Math.trunc(toNumber(p.qty))), 0),
    grossWeight: roundWeight(weights.reduce((s, w) => s + w.gross, 0)), volumetricWeight: roundWeight(weights.reduce((s, w) => s + w.volume, 0)),
    chargeableWeight: shipmentWeight(weights.reduce((s, w) => s + w.chargeable, 0), rule) };
}

/** DocToPackRule: chứng từ vượt 2kg phải khai như hàng hóa. */
export const isDocOverweight = (type: 'DOC' | 'PACK', grossWeight: string): boolean =>
  type === 'DOC' && toNumber(grossWeight) > RULES.docMaxWeightKg;

export interface CargoTypeState {
  type: 'DOC' | 'PACK';
  /** PACK do hệ thống tự chuyển (chứng từ quá cân) — khác PACK do khách tự chọn. */
  autoConverted: boolean;
}

/**
 * Áp quy tắc DOC ↔ PACK theo cân nặng khai ở "Thông tin đơn hàng":
 * - Chứng từ > 2kg → tự chuyển PACK (đánh dấu autoConverted).
 * - PACK do hệ thống tự chuyển mà cân giảm về ≤ 2kg → trả lại chứng từ.
 * - PACK khách tự chọn thì giữ nguyên dù cân nhẹ.
 */
export function applyDocWeightRule(
  state: CargoTypeState,
  grossWeight: string
): CargoTypeState & { change: 'toPack' | 'toDoc' | null } {
  if (isDocOverweight(state.type, grossWeight)) return { type: 'PACK', autoConverted: true, change: 'toPack' };
  if (state.type === 'PACK' && state.autoConverted && toNumber(grossWeight) <= RULES.docMaxWeightKg)
    return { type: 'DOC', autoConverted: false, change: 'toDoc' };
  return { ...state, change: null };
}

export const lineTotal = (item: Pick<InvoiceItemValues, 'qty' | 'price'>): number => round(toNumber(item.qty) * toNumber(item.price));

/** Tổng invoice gồm tiền hàng và shipping fee nếu khách khai. */
export const invoiceTotal = (items: ReadonlyArray<InvoiceItemValues>, shippingFee?: string): number =>
  round(items.reduce((s, it) => s + lineTotal(it), 0) + toNumber(shippingFee));

/** Tên hàng của 1 dòng kiện: "Nhiều loại hàng" → mô tả khách nhập; nhóm khác → tên nhóm. */
export function packageGoodsName(p: Pick<PackageValues, 'category' | 'description'>): string {
  const category = (p.category ?? '').trim();
  return category && category !== MULTI_CATEGORY ? category : (p.description ?? '').trim();
}

/**
 * Nội dung hàng (tên hàng ghi vào đơn) = tên hàng các dòng kiện, bỏ trùng, nối bằng ", ".
 * Nháp cũ khai nhóm hàng ở cấp đơn (chưa có theo dòng kiện) thì dùng cách cũ: nhóm thường → tên nhóm, nhiều loại → mô tả.
 */
export function goodsName(v: Pick<CreateOrderValues, 'goods' | 'packages'>): string {
  // Mô tả tổng quan (content) khách khai ở "Thông tin đơn hàng" là tên hàng của đơn
  const content = (v.goods.description ?? '').trim();
  if (content && !v.goods.category) return content;
  const names = [...new Set(v.packages.map(packageGoodsName).filter(Boolean))];
  return names.length ? names.join(', ') : packageGoodsName(v.goods);
}

/** Dữ liệu gửi POST /drafts. 'ready' = đã khai đủ, chờ in; 'draft' = lưu nháp dở. */
export function buildDraftPayload(v: CreateOrderValues, status: 'draft' | 'ready') {
  const isPack = v.shipment.type === 'PACK';
  const weight = isPack ? summarizePackages(v.packages, v.service).chargeableWeight || toNumber(v.shipment.grossWeight) : toNumber(v.shipment.grossWeight);
  return {
    stt: status,
    cnee: v.receiver.company || '(Chưa đặt tên)',
    ct: v.receiver.country || '—',
    service: v.service.hub || v.service.carrier || '—',
    branch: v.shipper.branch,
    ref: v.service.reference,
    pcs: fill('{pcs} kiện · {kg} kg', { pcs: (isPack ? summarizePackages(v.packages).pieces : toNumber(v.shipment.pieces)) || 1, kg: weight }),
    content: isPack ? goodsName(v) || 'Hàng hóa' : v.goods.docContent || 'Documents',
    payload: v as unknown as Record<string, unknown>
  };
}
