import type { Session } from '../types';

/** Mã quyền do backend cấp — trùng `*Permissions` ở backend (Contracts của từng module). */
export const PERMISSIONS = {
  shipmentsView: 'shipments.view',
  shipmentsCreate: 'shipments.create',
  shipmentsUpdate: 'shipments.update',
  shipmentsIssueBill: 'shipments.issue-bill',
  /** Tài khoản con xem mọi đơn của công ty (không có thì chỉ thấy đơn mình tạo). */
  shipmentsViewAll: 'shipments.view-all',
  ecommerceView: 'ecommerce.view',
  /** Tạo & xử lý đơn E-com: nhập tay, nhập CSV, sửa, xác nhận, xóa, đồng bộ. */
  ecommerceOrders: 'ecommerce.orders',
  ecommerceConnect: 'ecommerce.connect',
  /** Tài khoản con xem mọi đơn E-com của công ty (không có thì chỉ thấy đơn mình tạo). */
  ecommerceViewAll: 'ecommerce.view-all',
  manageStaff: 'account.staff',
  myTracking: 'account.mytracking',
  /** Tự đổi mật khẩu — tài khoản con không có (admin đặt lại). */
  changePassword: 'account.password'
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

/**
 * Phiên có quyền `permission` không. Không yêu cầu quyền → luôn được.
 * Backend chưa bật đăng nhập (`open`) hoặc chưa trả danh sách quyền → cho qua như trước (backend vẫn kiểm tra lại).
 */
export function can(session: Session | undefined, permission?: string): boolean {
  if (!permission) return true;
  if (!session || session.status === 'anonymous') return false;
  if (session.status === 'open') return true;
  const { permissions } = session.user;
  return permissions === undefined || permissions.includes(permission);
}
