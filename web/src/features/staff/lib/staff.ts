import { PERMISSIONS } from '@/features/auth';
import type { AssignablePermission, StaffAccount, StaffProfile } from '../types';

/** Nhóm quyền theo module (phần trước dấu chấm của mã quyền) để hiển thị ô chọn. */
export const PERMISSION_GROUPS: Readonly<Record<string, string>> = {
  shipments: 'Vận đơn & tạo đơn',
  ecommerce: 'E-commerce'
};

export interface PermissionGroup {
  key: string;
  label: string;
  items: AssignablePermission[];
}

/** Gom quyền theo module, giữ thứ tự backend trả; module chưa khai nhãn hiện mã module. */
export function groupPermissions(list: readonly AssignablePermission[]): PermissionGroup[] {
  const groups = new Map<string, PermissionGroup>();
  for (const p of list) {
    const key = p.code.split('.')[0] ?? p.code;
    const group = groups.get(key) ?? { key, label: PERMISSION_GROUPS[key] ?? key, items: [] };
    group.items.push(p);
    groups.set(key, group);
  }
  return [...groups.values()];
}

/**
 * Quyền phụ thuộc: tạo / sửa / in đơn đều cần xem đơn, kết nối shop cần xem shop.
 * Chọn quyền con thì tự thêm quyền xem; bỏ quyền xem thì bỏ luôn quyền con.
 */
const REQUIRES: Readonly<Record<string, string>> = {
  [PERMISSIONS.shipmentsCreate]: PERMISSIONS.shipmentsView,
  [PERMISSIONS.shipmentsUpdate]: PERMISSIONS.shipmentsView,
  [PERMISSIONS.shipmentsIssueBill]: PERMISSIONS.shipmentsView,
  [PERMISSIONS.shipmentsViewAll]: PERMISSIONS.shipmentsView,
  [PERMISSIONS.ecommerceOrders]: PERMISSIONS.ecommerceView,
  [PERMISSIONS.ecommerceViewAll]: PERMISSIONS.ecommerceView,
  [PERMISSIONS.ecommerceConnect]: PERMISSIONS.ecommerceView
};

export function togglePermission(current: readonly string[], code: string, checked: boolean): string[] {
  const next = new Set(current);
  if (checked) {
    next.add(code);
    const required = REQUIRES[code];
    if (required) next.add(required);
  } else {
    next.delete(code);
    for (const [child, parent] of Object.entries(REQUIRES)) if (parent === code) next.delete(child);
  }
  return [...next].sort();
}

/** Ô để trống gửi null; cắt khoảng trắng. */
export function toProfile(values: {
  fullName: string;
  email: string;
  phone: string;
  phone2: string;
  email2: string;
  permissions: string[];
}): StaffProfile {
  const blank = (v: string) => (v.trim() ? v.trim() : null);
  return {
    fullName: values.fullName.trim(),
    email: blank(values.email),
    phone: blank(values.phone),
    phone2: blank(values.phone2),
    email2: blank(values.email2),
    permissions: values.permissions
  };
}

/** Hồ sơ hiện có của nhân viên — gửi kèm khi chỉ khóa / mở khóa để không xóa mất SĐT, email. */
export function profileOf(s: StaffAccount): StaffProfile {
  return {
    fullName: s.fullName,
    email: s.email ?? null,
    phone: s.phone ?? null,
    phone2: s.phone2 ?? null,
    email2: s.email2 ?? null,
    permissions: s.permissions
  };
}

export type StaffFilter = 'all' | 'active' | 'locked';

/** Lọc theo trạng thái + tìm theo họ tên / tên đăng nhập / email / SĐT (không phân biệt hoa thường, dấu). */
export function filterStaff(list: readonly StaffAccount[], filter: StaffFilter, query: string): StaffAccount[] {
  const fold = (v: string) => v.normalize('NFD').replace(/\p{Diacritic}/gu, '').replace(/đ/gi, 'd').toLowerCase();
  const q = fold(query.trim());
  return list.filter(s => {
    if (filter === 'active' && !s.active) return false;
    if (filter === 'locked' && s.active) return false;
    return !q || [s.fullName, s.userName, s.email ?? '', s.phone ?? '', s.email2 ?? '', s.phone2 ?? ''].some(v => fold(v).includes(q));
  });
}

/** Số liệu đầu trang. */
export function staffStats(list: readonly StaffAccount[]) {
  const active = list.filter(s => s.active).length;
  return { total: list.length, active, locked: list.length - active };
}

/** Quyền đã cấp gom theo nhóm module (vd "Vận đơn & tạo đơn": [Xem đơn, Tạo đơn]) để hiện gọn trên thẻ nhân viên. */
export function grantedGroups(granted: readonly string[], describe: (code: string) => string): { label: string; items: string[] }[] {
  const groups = new Map<string, string[]>();
  for (const code of granted) {
    const key = code.split('.')[0] ?? code;
    const label = PERMISSION_GROUPS[key] ?? key;
    groups.set(label, [...(groups.get(label) ?? []), describe(code)]);
  }
  return [...groups].map(([label, items]) => ({ label, items }));
}
