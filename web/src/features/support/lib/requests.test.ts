import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '../types';
import { compactTime, defaultFilter, filterCounts, filterTickets } from './requests';

const ticket = (over: Partial<SupportTicket>): SupportTicket => ({
  id: 'TRB-1', kind: 'ho_tro', bill: '94012388', cnee: 'Kim', ct: 'Úc', type: 'Thất lạc kiện', lv: 'low',
  desc: 'Chỉ nhận 2/3 kiện', req: 'Hà', contact: '0909', date: '26/10/2026 08:40', status: 'new', ...over
});

describe('bảng yêu cầu đã gửi', () => {
  const tickets = [
    ticket({ id: 'TRB-1', status: 'waitc', date: '20/10/2026 08:00', updatedAt: '27/10/2026 09:00' }),
    ticket({ id: 'TRB-2', status: 'doing', date: '25/10/2026 08:00' }),
    ticket({ id: 'TRB-3', status: 'done', date: '26/10/2026 08:00' })
  ];

  it('tab mở đầu: Chưa xong → Tất cả', () => {
    expect(defaultFilter(tickets)).toBe('open');
    expect(defaultFilter([tickets[2]!])).toBe('all');
  });

  it('lọc, đếm, CS vừa cập nhật lên đầu', () => {
    expect(filterCounts(tickets)).toEqual({ open: 2, closed: 1, all: 3 });
    expect(filterTickets(tickets, 'all', '').map(t => t.id)).toEqual(['TRB-1', 'TRB-3', 'TRB-2']);
    expect(filterTickets(tickets, 'closed', '').map(t => t.id)).toEqual(['TRB-3']);
    expect(filterTickets(tickets, 'all', 'trb-2').map(t => t.id)).toEqual(['TRB-2']);
    // Tìm theo người nhận / tiêu đề, không phân biệt hoa thường
    const more = [...tickets, ticket({ id: 'TRB-5', bill: '', cnee: 'Müller', subject: 'Không in được nhãn', status: 'new' })];
    expect(filterTickets(more, 'all', 'IN ĐƯỢC').map(t => t.id)).toEqual(['TRB-5']);
    expect(filterTickets(more, 'all', 'müller').map(t => t.id)).toEqual(['TRB-5']);
  });

  it('giờ gọn: cùng năm bỏ năm, khác năm bỏ giờ', () => {
    const today = new Date(2026, 9, 28);
    expect(compactTime('26/10/2026 08:40', today)).toBe('26/10 08:40');
    expect(compactTime('30/12/2025 17:00', today)).toBe('30/12/2025');
    expect(compactTime('', today)).toBe('');
  });
});
