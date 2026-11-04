/**
 * Trang mở ra sau khi đăng nhập / bấm "Vào portal" (không còn trang chủ riêng): /home chuyển tiếp theo quyền —
 * có quyền tạo đơn → Tạo đơn hàng, không có (tài khoản con chỉ xem) → Đơn hàng của tôi.
 */
export const PORTAL_HOME = '/home';

import type { SessionUser } from '../types';

/** Nhân viên Việt An chọn / đổi khách để làm thay. */
export const CHOOSE_CUSTOMER_PATH = '/chon-khach';

/** Các trang nhân viên Việt An vào được khi chưa chọn khách. */
const NO_CUSTOMER_PATHS = new Set([CHOOSE_CUSTOMER_PATH, '/troubles']);

/** Nhân viên Việt An (dbo.USERS) đang đăng nhập portal để làm thay khách. */
export function isVietAnAgent(user: SessionUser | null | undefined): boolean {
  return user?.accountType === 'vietan';
}

/** Nhân viên Việt An chưa chọn khách mà mở trang của khách → phải chọn khách trước. Hàm thuần — có test. */
export function needsCustomer(user: SessionUser | null | undefined, pathname: string): boolean {
  return isVietAnAgent(user) && !user?.customerCode && !NO_CUSTOMER_PATHS.has(pathname);
}

/** Các trang ngoài (không cần đăng nhập) — không dùng làm đích quay lại. */
const PUBLIC_PATHS = new Set(['/', '/login', CHOOSE_CUSTOMER_PATH]);

/**
 * Đích chuyển tới sau đăng nhập, lấy từ `?next=`.
 * Chỉ chấp nhận đường dẫn nội bộ (bắt đầu bằng 1 dấu "/") để tránh chuyển hướng ra site lạ.
 */
export function safeNextPath(next: string | null | undefined): string {
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.startsWith('/\\')) return PORTAL_HOME;
  const path = next.split(/[?#]/)[0] ?? next;
  return PUBLIC_PATHS.has(path) ? PORTAL_HOME : next;
}

/** Đường dẫn trang đăng nhập, kèm trang cần quay lại. */
export function loginPath(from?: string): string {
  const next = from && safeNextPath(from) !== PORTAL_HOME ? from : undefined;
  return next ? `/login?next=${encodeURIComponent(next)}` : '/login';
}

/** Chữ viết tắt cho avatar: bỏ tiền tố loại hình công ty, lấy 2 chữ cái đầu của tên. */
export function initialsOf(name: string | undefined): string {
  const words = (name ?? '')
    .replace(/^(công ty|cty)\s+(tnhh|cổ phần|cp)?\s*(mtv|một thành viên)?/iu, '')
    // Đuôi pháp lý tiếng Anh: "SCS CO., LTD" → "SCS"
    .replace(/[\s,.]+(co\.?,?\s*ltd|company\s+limited|ltd|jsc|llc|inc|corp|co)\.?\s*$/iu, '')
    .split(/[\s,]+/)
    .filter(Boolean);
  const first = words[0];
  const last = words[words.length - 1];
  if (!first || !last) return '';
  const letters = words.length === 1 ? first.slice(0, 2) : first.charAt(0) + last.charAt(0);
  return letters.toLocaleUpperCase('vi');
}
