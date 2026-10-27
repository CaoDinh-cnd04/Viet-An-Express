/**
 * Cỡ chữ dòng địa chỉ ở đầu trang tracking: địa chỉ càng dài chữ càng nhỏ để cố nằm trên 1 hàng
 * (quá dài vẫn xuống dòng, không cắt chữ). Hàm thuần — có test.
 */
export function addressSize(address: string): 'normal' | 'long' | 'veryLong' {
  const length = address.trim().length;
  if (length > 100) return 'veryLong';
  if (length > 65) return 'long';
  return 'normal';
}
