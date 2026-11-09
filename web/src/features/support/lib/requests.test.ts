import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '../types';
import { compactTime, defaultFilter, filterCounts, filterTickets } from './requests';

const ticket = (over: Partial<SupportTicket>): SupportTicket => ({
  id: 'TRB-1', kind: 'ho_tro', bill: '94012388', cnee: 'Kim', ct: 'Úc', type: 'Thất lạc kiện', lv: 'low',
  desc: 'Chỉ nhận 2/3 kiện', req: 'Hà', contact: '0909', date: '26/10/2026 08:40', status: 'new', ...over
});

const ids = (list: SupportTicket[]) => list.map(t => t.id);

describe('danh sách yêu cầu của khách', () => {
  const today = new Date(2026, 9, 28);
  const tickets = [
    ticket({ id: 'TRB-1', status: 'waitc', date: '20/10/2026 08:00', updatedAt: '27/10/2026 09:00' }),
    ticket({ id: 'TRB-2', status: 'doing', date: '25/10/2026 08:00' }),
    ticket({ id: 'TRB-3', status: 'done', date: '26/10/2026 08:00' }),
    ticket({ id: 'TRB-4', status: 'new', kind: 'gop_y', bill: '', date: '02/09/2026 10:00' })
  ];

  it('ô mở đầu: Cần bạn trả lời → Đang xử lý → Tất cả', () => {
    expect(defaultFilter(tickets)).toBe('waitc');
    expect(defaultFilter([tickets[1]!, tickets[2]!])).toBe('doing');
    expect(defaultFilter([tickets[2]!])).toBe('all');
  });

  it('đếm theo ô: "Đang xử lý" gồm cả yêu cầu mới', () => {
    expect(filterCounts(tickets)).toEqual({ waitc: 1, doing: 2, closed: 1, all: 4 });
    expect(ids(filterTickets(tickets, { filter: 'doing' }, today))).toEqual(['TRB-2', 'TRB-4']);
    expect(ids(filterTickets(tickets, { filter: 'closed' }, today))).toEqual(['TRB-3']);
  });

  it('mới hoạt động trước (CS vừa trả lời lên đầu)', () => {
    expect(ids(filterTickets(tickets, { filter: 'all' }, today))).toEqual(['TRB-1', 'TRB-3', 'TRB-2', 'TRB-4']);
  });

  it('lọc loại: hỗ trợ gồm sự cố cũ, góp ý riêng', () => {
    const old = ticket({ id: 'TRB-9', kind: undefined });
    expect(ids(filterTickets([...tickets, old], { filter: 'all', kind: 'gop_y' }, today))).toEqual(['TRB-4']);
    expect(ids(filterTickets([...tickets, old], { filter: 'all', kind: 'ho_tro' }, today))).not.toContain('TRB-4');
    expect(ids(filterTickets([...tickets, old], { filter: 'all', kind: 'ho_tro' }, today))).toContain('TRB-9');
  });

  it('lọc thời gian theo lần cập nhật gần nhất', () => {
    expect(ids(filterTickets(tickets, { filter: 'all', period: '7d' }, today))).toEqual(['TRB-1', 'TRB-3', 'TRB-2']);
    expect(ids(filterTickets(tickets, { filter: 'all', period: 'month' }, today))).toHaveLength(3);
    expect(ids(filterTickets(tickets, { filter: 'all', period: '30d' }, today))).toHaveLength(3);
  });

  it('tìm theo mã, người nhận, tiêu đề — không phân biệt hoa thường', () => {
    expect(ids(filterTickets(tickets, { filter: 'all', query: 'trb-2' }, today))).toEqual(['TRB-2']);
    const more = [...tickets, ticket({ id: 'TRB-5', bill: '', cnee: 'Müller', subject: 'Không in được nhãn', status: 'new' })];
    expect(ids(filterTickets(more, { filter: 'all', query: 'IN ĐƯỢC' }, today))).toEqual(['TRB-5']);
    expect(ids(filterTickets(more, { filter: 'all', query: 'müller' }, today))).toEqual(['TRB-5']);
  });

  it('giờ gọn: cùng năm bỏ năm, khác năm bỏ giờ', () => {
    expect(compactTime('26/10/2026 08:40', today)).toBe('26/10 08:40');
    expect(compactTime('30/12/2025 17:00', today)).toBe('30/12/2025');
    expect(compactTime('', today)).toBe('');
  });
});
