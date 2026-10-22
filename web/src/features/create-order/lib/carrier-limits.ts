import { fill } from '@/shared/i18n';
import type { PackageValues } from '../schema';
import { summarizePackages, toNumber } from './shipment';

export interface CarrierLimit {
  /** Cạnh dài tối đa trước khi bị phụ phí quá khổ (cm) */
  maxSide: number;
  /** Tổng 3 cạnh tối đa (cm) */
  maxSum: number;
  /** Kiện từ mức cân này trở lên (≥, kg) bị phụ phí hàng nặng */
  heavyWeight: number;
  /** Vượt mức này hãng không nhận (cm / kg) */
  nonSide: number;
  nonWeight: number;
}

/** Kiện nặng từ 25kg trở lên bị phụ thu phí xử lý hàng nặng — áp chung các hãng chuyển phát (trừ SEA). */
export const HEAVY_PIECE_KG = 25;

/**
 * Giới hạn kích thước / cân nặng theo hãng — lấy theo quy định của Việt An
 * (đề xuất endpoint GET /services/limits — docs/API_CONTRACT.md §3). Khi backend có API, thay bằng dữ liệu API.
 */
export const CARRIER_LIMITS: Record<string, CarrierLimit> = {
  _default: { maxSide: 120, maxSum: 300, heavyWeight: HEAVY_PIECE_KG, nonSide: 200, nonWeight: 100 },
  DHL: { maxSide: 120, maxSum: 300, heavyWeight: HEAVY_PIECE_KG, nonSide: 120, nonWeight: 70 },
  Fedex: { maxSide: 121, maxSum: 330, heavyWeight: HEAVY_PIECE_KG, nonSide: 121, nonWeight: 68 },
  UPS: { maxSide: 122, maxSum: 330, heavyWeight: HEAVY_PIECE_KG, nonSide: 122, nonWeight: 70 },
  Aramex: { maxSide: 120, maxSum: 300, heavyWeight: HEAVY_PIECE_KG, nonSide: 150, nonWeight: 70 },
  'Chuyên tuyến': { maxSide: 150, maxSum: 330, heavyWeight: HEAVY_PIECE_KG, nonSide: 220, nonWeight: 120 },
  Ecommerce: { maxSide: 100, maxSum: 250, heavyWeight: HEAVY_PIECE_KG, nonSide: 150, nonWeight: 50 },
  SEA: { maxSide: 300, maxSum: 600, heavyWeight: 1000, nonSide: 600, nonWeight: 2000 }
};

export type WarningLevel = 'critical' | 'warning' | 'info';

export interface PackageWarning {
  level: WarningLevel;
  title: string;
  detail: string;
}

const fmt = (n: number) => String(Math.round(n * 10) / 10);

/** Cảnh báo quá khổ / quá tải cho từng dòng kiện + gợi ý khi cước tính theo quy đổi. */
export function evaluatePackages(carrier: string, packages: ReadonlyArray<PackageValues>): PackageWarning[] {
  const limit = CARRIER_LIMITS[carrier] ?? CARRIER_LIMITS._default!;
  const out: PackageWarning[] = [];

  // Câu có {biến}: tiếng Việt điền số ở đây, giao diện dịch theo mẫu câu (xem shared/i18n).
  packages.forEach((p, idx) => {
    const [d, w, h, g] = [toNumber(p.length), toNumber(p.width), toNumber(p.height), toNumber(p.weight)];
    if (!d && !w && !h && !g) return;
    const longest = Math.max(d, w, h);
    const sum = d + w + h;
    const i = idx + 1;

    if (longest > limit.nonSide || g > limit.nonWeight) {
      out.push({
        level: 'critical',
        title: carrier ? fill('Kiện {i}: vượt giới hạn nhận của {carrier}', { i, carrier }) : fill('Kiện {i}: vượt giới hạn nhận của hãng', { i }),
        detail: fill('Cạnh dài {side}cm / cân {kg}kg vượt mức tối đa. Cần chia nhỏ kiện hoặc chuyển sang Chuyên tuyến / SEA.', { side: fmt(longest), kg: fmt(g) })
      });
      return;
    }
    if (longest > limit.maxSide) {
      out.push({
        level: 'warning',
        title: fill('Kiện {i}: hàng quá khổ', { i }),
        detail: fill('Cạnh dài {side}cm > {max}cm — có thể bị phụ phí hàng cồng kềnh.', { side: fmt(longest), max: limit.maxSide })
      });
    }
    if (sum > limit.maxSum) {
      out.push({
        level: 'warning',
        title: fill('Kiện {i}: tổng kích thước lớn', { i }),
        detail: fill('D+R+C = {sum}cm > {max}cm — có thể bị phụ phí quá khổ.', { sum: fmt(sum), max: limit.maxSum })
      });
    }
    if (g >= limit.heavyWeight) {
      out.push({
        level: 'warning',
        title: fill('Kiện {i}: quá nặng', { i }),
        detail: fill('Cân {kg}kg ≥ {max}kg/kiện — phụ phí xử lý hàng nặng.', { kg: fmt(g), max: limit.heavyWeight })
      });
    }
  });

  const s = summarizePackages(packages, carrier);
  if (s.grossWeight > 0 && s.volumetricWeight > s.grossWeight + 0.01) {
    out.push({
      level: 'info',
      title: 'Cước tính theo trọng lượng quy đổi',
      detail: fill('Quy đổi {vol}kg > cân thực {kg}kg. Đóng gói gọn hơn để giảm cước.', { vol: fmt(s.volumetricWeight), kg: fmt(s.grossWeight) })
    });
  }
  return out;
}

export const hasCritical = (warnings: ReadonlyArray<PackageWarning>) => warnings.some(w => w.level === 'critical');
