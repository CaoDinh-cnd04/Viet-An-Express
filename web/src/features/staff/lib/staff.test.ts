import { describe, expect, it } from 'vitest';
import { formatIsoDateTime } from '@/shared/lib/format';
import type { StaffAccount } from '../types';
import { filterStaff, grantedGroups, groupPermissions, staffStats, profileOf, togglePermission, toProfile } from './staff';

describe('groupPermissions', () => {
  it('gom theo module, giữ thứ tự và nhãn tiếng Việt', () => {
    const groups = groupPermissions([
      { code: 'ecommerce.view', description: 'Xem shop' },
      { code: 'shipments.create', description: 'Tạo đơn' },
      { code: 'shipments.view', description: 'Xem đơn' },
      { code: 'pickups.create', description: 'Đặt pickup' }
    ]);
    expect(groups.map(g => [g.label, g.items.map(i => i.code)])).toEqual([
      ['E-commerce', ['ecommerce.view']],
      ['Vận đơn & tạo đơn', ['shipments.create', 'shipments.view']],
      ['pickups', ['pickups.create']]
    ]);
  });
});

describe('togglePermission', () => {
  it('chọn quyền tạo đơn thì tự thêm quyền xem đơn', () => {
    expect(togglePermission([], 'shipments.create', true)).toEqual(['shipments.create', 'shipments.view']);
  });

  it('bỏ quyền xem thì bỏ luôn quyền phụ thuộc', () => {
    const current = ['ecommerce.view', 'shipments.create', 'shipments.issue-bill', 'shipments.view'];
    expect(togglePermission(current, 'shipments.view', false)).toEqual(['ecommerce.view']);
  });

  it('chọn xem toàn bộ đơn của công ty thì tự thêm quyền xem đơn', () => {
    expect(togglePermission([], 'shipments.view-all', true)).toEqual(['shipments.view', 'shipments.view-all']);
    expect(togglePermission(['shipments.view', 'shipments.view-all'], 'shipments.view', false)).toEqual([]);
  });

  it('quyền E-commerce: tạo đơn / xem toàn bộ cần quyền xem, bỏ quyền xem thì bỏ hết', () => {
    expect(togglePermission([], 'ecommerce.orders', true)).toEqual(['ecommerce.orders', 'ecommerce.view']);
    expect(togglePermission(['ecommerce.orders', 'ecommerce.view', 'ecommerce.view-all'], 'ecommerce.view', false)).toEqual([]);
  });

  it('bỏ quyền con không ảnh hưởng quyền xem', () => {
    expect(togglePermission(['shipments.create', 'shipments.view'], 'shipments.create', false)).toEqual(['shipments.view']);
  });
});

describe('toProfile', () => {
  it('cắt khoảng trắng, ô trống thành null', () => {
    expect(toProfile({ fullName: ' Lan ', email: ' ', phone: ' 0901 ', phone2: '', email2: ' lan2@sgb.vn ', permissions: ['shipments.view'] }))
      .toEqual({ fullName: 'Lan', email: null, phone: '0901', phone2: null, email2: 'lan2@sgb.vn', permissions: ['shipments.view'] });
  });

  it('khóa / mở khóa giữ nguyên SĐT, email thứ 2', () => {
    const s = { id: 1, userName: 'lan', fullName: 'Lan', phone: '0901', phone2: '0912', email2: 'b@x.vn', permissions: [], active: true, createdAt: '' };
    expect(profileOf(s)).toEqual({ fullName: 'Lan', email: null, phone: '0901', phone2: '0912', email2: 'b@x.vn', permissions: [] });
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
  id: 1, userName: 'kho.hn', fullName: 'Nguyễn Văn Kho', email: 'kho@sgb.vn', phone: '0909', permissions: [], active: true, createdAt: '', ...over
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

  it('gom quyền đã cấp theo nhóm', () => {
    const names: Record<string, string> = { 'shipments.view': 'Xem đơn', 'shipments.create': 'Tạo đơn', 'ecommerce.view': 'Xem shop' };
    expect(grantedGroups(['shipments.view', 'shipments.create', 'ecommerce.view'], c => names[c]!)).toEqual([
      { label: 'Vận đơn & tạo đơn', items: ['Xem đơn', 'Tạo đơn'] },
      { label: 'E-commerce', items: ['Xem shop'] }
    ]);
  });
});
