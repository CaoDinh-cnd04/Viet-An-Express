/**
 * Site cũ chứa ảnh / tệp CRM chèn vào thông báo popup (TConfig.ThongBao_NoiDung tham chiếu "/UserFile/editor/images/…").
 * Kiểm tra 30/10/2026: ảnh có ở vietanexpress.com.vn (mytracking.top trả 404). Đổi lúc build bằng VITE_LEGACY_SITE_URL.
 */
export const LEGACY_SITE_URL: string = (import.meta.env.VITE_LEGACY_SITE_URL ?? 'https://vietanexpress.com.vn').replace(/\/+$/, '');
