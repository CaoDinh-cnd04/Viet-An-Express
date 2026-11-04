/** Số chữ số của mã OTP đăng nhập nhân viên Việt An. */
export const OTP_LENGTH = 6;

/**
 * Điền chữ khách gõ / dán vào các ô OTP bắt đầu từ ô `index`: chỉ lấy chữ số (bỏ khoảng trắng, gạch, chữ "Mã:"…),
 * dán đủ 6 số thì điền lại từ ô đầu. Trả mảng ô mới + ô cần focus tiếp. Hàm thuần — có test.
 */
export function fillOtp(current: readonly string[], index: number, text: string): { digits: string[]; focus: number } {
  const input = text.replace(/\D/g, '');
  const digits = Array.from({ length: OTP_LENGTH }, (_, i) => current[i] ?? '');
  if (!input) return { digits, focus: index };
  const start = input.length >= OTP_LENGTH ? 0 : index;
  const chars = input.slice(0, OTP_LENGTH - start).split('');
  chars.forEach((c, i) => { digits[start + i] = c; });
  return { digits, focus: Math.min(start + chars.length, OTP_LENGTH - 1) };
}
