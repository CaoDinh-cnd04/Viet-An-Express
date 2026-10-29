/**
 * Dấu của nội dung thông báo popup CRM (TConfig.ThongBao_NoiDung) — lưu dấu đã đóng thay vì cả HTML (có thể rất dài).
 * CRM sửa nội dung → dấu đổi → popup hiện lại. Hàm thuần (FNV-1a 32 bit) — có test.
 */
export function popupSignature(html: string): string {
  let hash = 0x811c9dc5;
  for (let i = 0; i < html.length; i++) {
    hash ^= html.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return `${html.length.toString(36)}.${hash.toString(36)}`;
}
