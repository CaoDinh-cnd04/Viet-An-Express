/** Số có tối đa `max` chữ số sau dấu chấm (chuỗi rỗng / không phải số → false). Hàm thuần — có test. */
export function hasMaxDecimals(value: string, max: number): boolean {
  const v = value.trim();
  if (!/^\d+(\.\d*)?$/.test(v)) return false;
  const dot = v.indexOf('.');
  return dot < 0 || v.length - dot - 1 <= max;
}
