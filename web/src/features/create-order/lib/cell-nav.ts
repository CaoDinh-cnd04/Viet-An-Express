/** Ô sang trái / phải khi nhấn mũi tên trong bảng kiện hàng / invoice. */
export type CellMove = 'next' | 'prev' | null;

/**
 * Nhấn → khi con trỏ ở cuối ô (không bôi đen) → sang ô sau; ← khi con trỏ ở đầu ô → về ô trước.
 * Con trỏ ở giữa / đang bôi đen → không chuyển (mũi tên di chuyển con trỏ như bình thường). Hàm thuần — có test.
 */
export function arrowMove(key: string, start: number | null, end: number | null, length: number): CellMove {
  if (start === null || end === null || start !== end) return null;
  if (key === 'ArrowRight' && end === length) return 'next';
  if (key === 'ArrowLeft' && start === 0) return 'prev';
  return null;
}

/** Ô số dạng chữ (để biết vị trí con trỏ): chỉ giữ chữ số và 1 dấu thập phân ("," đổi thành "."). Số nguyên → chỉ chữ số. */
export function sanitizeDecimal(value: string, integer = false, maxDecimals?: number): string {
  const digits = value.replace(',', '.').replace(integer ? /\D/g : /[^\d.]/g, '');
  if (integer) return digits;
  const dot = digits.indexOf('.');
  if (dot < 0) return digits;
  const fraction = digits.slice(dot + 1).replace(/\./g, '');
  // Cắt bớt số lẻ vượt giới hạn (vd đơn giá invoice chỉ 2 số lẻ theo chuẩn quốc tế).
  return digits.slice(0, dot + 1) + (maxDecimals === undefined ? fraction : fraction.slice(0, maxDecimals));
}

