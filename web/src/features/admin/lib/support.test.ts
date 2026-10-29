import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '@/features/support';
import type { AdminSupportTicket } from '../api';
import { adminKindLabel, filterSupportTickets, isContactMessage, matchesStaff, staffOptions, ticketActions, UNASSIGNED } from './support';

const item = (over: Partial<SupportTicket>, customer = 'KH01'): AdminSupportTicket => ({
  ticket: {
    id: 'TRB-1', bill: 'VAHCM1', cnee: 'John', ct: 'US', type: 'Giao chậm / trễ hẹn', lv: 'low', desc: 'Hàng chưa tới',
    req: 'An', contact: '', date: '10/10/2026 09:00', status: 'new', ...over
  },
  customerCode: customer,
  companyName: 'Công ty Minh Phát',
  isStaff: false
});

describe('filterSupportTickets', () => {
  const items = [
    item({ id: 'TRB-1', status: 'new' }),
    item({ id: 'TRB-2', status: 'waitc', kind: 'ho_tro', bill: '', subject: 'Xin bảng giá' }, 'KH02'),
    item({ id: 'TRB-3', status: 'done' })
  ];

  it('"Chưa xong" bỏ đơn đã xử lý, "Tất cả" giữ hết', () => {
    expect(filterSupportTickets(items, 'open', '').map(x => x.ticket.id)).toEqual(['TRB-1', 'TRB-2']);
    expect(filterSupportTickets(items, 'all', '')).toHaveLength(3);
    expect(filterSupportTickets(items, 'waitc', '').map(x => x.ticket.id)).toEqual(['TRB-2']);
  });

  it('tìm không dấu theo mã khách, tiêu đề, loại', () => {
    expect(filterSupportTickets(items, 'all', 'kh02').map(x => x.ticket.id)).toEqual(['TRB-2']);
    expect(filterSupportTickets(items, 'all', 'bang gia').map(x => x.ticket.id)).toEqual(['TRB-2']);
    expect(filterSupportTickets(items, 'all', 'su co van don').map(x => x.ticket.id)).toEqual(['TRB-1', 'TRB-3']);
  });
});

describe('lọc theo nhân viên phụ trách', () => {
  const items = [
    { ...item({ id: 'TRB-1' }), managerId: 15, managerName: 'Nguyễn Ngọc Hân' },
    { ...item({ id: 'TRB-2' }), managerId: 9, managerName: 'Bùi Minh Tú' },
    { ...item({ id: 'TRB-3' }), managerId: 15, managerName: 'Nguyễn Ngọc Hân' },
    item({ id: 'TRB-4' })
  ];
  const ids = (staff: string) => items.filter(x => matchesStaff(x, staff)).map(x => x.ticket.id);

  it('tất cả / 1 nhân viên / chưa phân công', () => {
    expect(ids('')).toHaveLength(4);
    expect(ids('15')).toEqual(['TRB-1', 'TRB-3']);
    expect(ids(UNASSIGNED)).toEqual(['TRB-4']);
  });

  it('danh sách nhân viên không trùng, xếp theo tên', () => {
    expect(staffOptions(items)).toEqual([{ id: '9', name: 'Bùi Minh Tú' }, { id: '15', name: 'Nguyễn Ngọc Hân' }]);
  });
});

describe('ticketActions', () => {
  it('mỗi trạng thái có đúng 1 nút chính, không có nút giữ nguyên trạng thái', () => {
    for (const s of ['new', 'doing', 'waitc', 'done'] as const) {
      const actions = ticketActions(s);
      expect(actions.filter(a => a.primary)).toHaveLength(1);
      expect(actions.some(a => a.status === s)).toBe(false);
    }
  });

  it('lời nhắn trang chủ không có nút "Chờ khách"', () => {
    expect(ticketActions('new', false).map(a => a.status)).toEqual(['done', 'doing']);
    expect(ticketActions('waitc', false).map(a => a.status)).toEqual(['doing', 'done']);
  });
});

describe('lời nhắn từ trang chủ', () => {
  it('hiện loại "Liên hệ trang chủ" và tìm được theo loại', () => {
    const contact = { ...item({ id: 'TRB-9', bill: '', type: 'Liên hệ từ trang chủ' }), customerCode: '', companyName: '' };
    (contact.ticket as { kind?: string }).kind = 'lien_he';
    expect(isContactMessage(contact.ticket)).toBe(true);
    expect(adminKindLabel(contact.ticket)).toBe('Liên hệ trang chủ');
    expect(filterSupportTickets([contact], 'all', 'lien he trang chu').map(x => x.ticket.id)).toEqual(['TRB-9']);
  });
});
