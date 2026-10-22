/**
 * Endpoint backend chưa làm (web/docs/API_CONTRACT.md ghi "Chưa làm"): `http` không gửi request mà báo "chưa có" ngay
 * (như backend trả 501) — các trang vẫn xử lý bằng isNotImplemented(), nhưng trình duyệt không hiện lỗi 404 trong console.
 * Backend làm xong endpoint nào thì xoá khỏi danh sách.
 */
export const PENDING_ENDPOINTS: readonly string[] = [
  '/ecom/settings', '/ecom/import-csv',
  '/addresses/senders', '/catalog/addon-fees', '/rates', '/services', '/public/contact'
];

/** Đường dẫn thuộc endpoint chưa làm: trùng hoặc nằm dưới 1 mục (bỏ query). Hàm thuần — có test. */
export function isPendingEndpoint(path: string): boolean {
  const clean = path.split('?')[0] ?? path;
  return PENDING_ENDPOINTS.some(p => clean === p || clean.startsWith(`${p}/`));
}
