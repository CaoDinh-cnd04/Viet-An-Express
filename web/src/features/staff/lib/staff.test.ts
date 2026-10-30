import { describe, expect, it } from 'vitest';
import { formatIsoDateTime } from '@/shared/lib/format';
import type { StaffAccount } from '../types';
import { filterStaff, staffStats, profileOf, toProfile } from './staff';

describe('toProfile', () => {
  it('cắt khoảng trắng, ô trống thành null', () => {
    expect(toProfile({ fullName: ' Lan ', email: ' ', phone: ' 0901 ', phone2: '', email2: ' lan2@sgb.vn ' }))
      .toEqual({ fullName: 'Lan', email: null, phone: '0901', phone2: null, email2: 'lan2@sgb.vn' });
  });

  it('khóa / mở khóa giữ nguyên SĐT, email thứ 2', () => {
    const s = { id: 1, userName: 'lan', fullName: 'Lan', phone: '0901', phone2: '0912', email2: 'b@x.vn', active: true, createdAt: '' };
    expect(profileOf(s)).toEqual({ fullName: 'Lan', email: null, phone: '0901', phone2: '0912', email2: 'b@x.vn' });
  });
});

describe('formatIsoDateTime', () => {
  it('đổi sang dd/mm/yyyy HH:mm, giá trị trống thì rỗng', () => {
    expect(formatIsoDateTime('2026-10-06T08:05:31')).toBe('06/10/2026 08:05');
    expect(formatIsoDateTime(null)).toBe('');
    expect(formatIsoDateTime('khác')).toBe('');
  });
});

const staff = (over: Partial<StaffAccount>): StaffAccount => ({
  id: 1, userName: 'kho.hn', fullName: 'Nguyễn Văn Kho', email: 'kho@sgb.vn', phone: '0909', active: true, createdAt: '', ...over
});

describe('trang tài khoản nhân viên', () => {
  it('lọc trạng thái và tìm không dấu theo tên / tên đăng nhập / email', () => {
    const list = [staff({ id: 1 }), staff({ id: 2, fullName: 'Trần Thị Đào', userName: 'dao.kt', email: null, active: false })];
    expect(filterStaff(list, 'active', '').map(s => s.id)).toEqual([1]);
    expect(filterStaff(list, 'locked', '').map(s => s.id)).toEqual([2]);
    expect(filterStaff(list, 'all', 'dao').map(s => s.id)).toEqual([2]);
    expect(filterStaff(list, 'all', 'KHO@').map(s => s.id)).toEqual([1]);
    expect(staffStats(list)).toEqual({ total: 2, active: 1, locked: 1 });
  });
});
