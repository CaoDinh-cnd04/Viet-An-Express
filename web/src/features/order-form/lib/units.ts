/** Đơn vị tính (ĐVT) có sẵn để chọn — theo hệ thống cũ. */
export const PRESET_UNITS = ['PCS', 'SET', 'BOX', 'Bag'] as const;
/** Lựa chọn "Khác": khách tự nhập ĐVT. */
export const OTHER_UNIT = 'Khác';
export const DEFAULT_UNIT = 'PCS';
/** Độ dài tối đa ĐVT khách tự nhập. */
export const UNIT_MAX = 20;

const findPreset = (unit: string) => PRESET_UNITS.find(u => u.toUpperCase() === unit.trim().toUpperCase());

/** Giá trị ô chọn ĐVT: đơn vị có sẵn, hoặc "Khác" khi khách tự nhập (kể cả đang để trống). */
export function unitChoice(unit: string): string {
  return findPreset(unit) ?? OTHER_UNIT;
}

/** ĐVT có sẵn trong danh sách (PCS, SET, BOX, Bag — không phân biệt hoa thường). */
export function isPresetUnit(unit: string): boolean {
  return findPreset(unit) !== undefined;
}

export const QTY_POSITIVE_MESSAGE = 'SL > 0';
export const QTY_INTEGER_MESSAGE = 'SL phải là số nguyên > 0';

/**
 * Lỗi số lượng 1 dòng invoice: ĐVT có sẵn (đếm được: PCS, SET, BOX, Bag) → số nguyên > 0;
 * ĐVT khách tự nhập (vd KG, M) → số thực > 0. Hợp lệ → undefined. Hàm thuần — có test.
 */
export function qtyError(qty: string, unit: string): string | undefined {
  const v = qty.trim();
  if (v === '' || !Number.isFinite(Number(v)) || Number(v) <= 0) return isPresetUnit(unit) ? QTY_INTEGER_MESSAGE : QTY_POSITIVE_MESSAGE;
  if (isPresetUnit(unit) && !/^\d+$/.test(v)) return QTY_INTEGER_MESSAGE;
  return undefined;
}

/** Chuẩn hóa ĐVT (vd khi nhập từ file): trùng đơn vị có sẵn thì viết đúng chuẩn, trống thì PCS, còn lại giữ chữ khách nhập. */
export function normalizeUnit(raw: string | undefined): string {
  const unit = (raw ?? '').trim();
  if (!unit) return DEFAULT_UNIT;
  return findPreset(unit) ?? unit.slice(0, UNIT_MAX);
}
