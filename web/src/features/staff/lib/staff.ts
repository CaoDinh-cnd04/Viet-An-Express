import type { StaffAccount, StaffProfile } from '../types';

/** Ô để trống gửi null; cắt khoảng trắng. */
export function toProfile(values: {
  fullName: string;
  email: string;
  phone: string;
  phone2: string;
  email2: string;
}): StaffProfile {
  const blank = (v: string) => (v.trim() ? v.trim() : null);
  return {
    fullName: values.fullName.trim(),
    email: blank(values.email),
    phone: blank(values.phone),
    phone2: blank(values.phone2),
    email2: blank(values.email2)
  };
}

/** Hồ sơ hiện có của nhân viên — gửi kèm khi chỉ khóa / mở khóa để không xóa mất SĐT, email. */
export function profileOf(s: StaffAccount): StaffProfile {
  return {
    fullName: s.fullName,
    email: s.email ?? null,
    phone: s.phone ?? null,
    phone2: s.phone2 ?? null,
    email2: s.email2 ?? null
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
