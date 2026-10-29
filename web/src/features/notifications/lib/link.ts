/** Giới hạn đường link thông báo — khớp backend (Identity/Notifications/Notification.cs). */
export const NOTIFICATION_LINK_MAX = 500;

/**
 * Link "Xem tại đây" an toàn để mở: chỉ http / https tuyệt đối (chặn javascript:, data:…). Không hợp lệ → null.
 * Backend đã kiểm khi lưu; frontend kiểm lại trước khi hiện nút. Hàm thuần — có test.
 */
export function safeLink(value?: string | null): string | null {
  const v = value?.trim() ?? '';
  if (!v || v.length > NOTIFICATION_LINK_MAX) return null;
  try {
    const url = new URL(v);
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.hostname ? v : null;
  } catch {
    return null;
  }
}
