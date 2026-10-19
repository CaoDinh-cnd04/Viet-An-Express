import { describe, expect, it } from 'vitest';
import type { TroubleTicket } from '../types';
import { filterTickets, kindOf, ticketSubline, ticketTitle } from './chat';

const ticket = (over: Partial<TroubleTicket>): TroubleTicket => ({
  id: 'TRB-1', bill: 'VAHCM1', cnee: 'John Doe', ct: 'US', type: 'Giao chậm / trễ hẹn', lv: 'low', desc: 'Hàng chưa tới',
  req: 'An', contact: '', date: '10/10/2026 09:00', status: 'new', ...over
});

describe('filterTickets', () => {
  const list = [ticket({ id: 'TRB-1', status: 'new' }), ticket({ id: 'TRB-2', bill: 'VAHCM2', cnee: 'Müller', status: 'done' })];

  it('"Chưa xong" bỏ sự cố đã xử lý, "Tất cả" giữ hết', () => {
    expect(filterTickets(list, 'open', '').map(t => t.id)).toEqual(['TRB-1']);
    expect(filterTickets(list, 'all', '')).toHaveLength(2);
    expect(filterTickets(list, 'done', '').map(t => t.id)).toEqual(['TRB-2']);
  });

  it('tìm theo mã ticket, vận đơn, người nhận — không phân biệt hoa thường', () => {
    expect(filterTickets(list, 'all', 'trb-2').map(t => t.id)).toEqual(['TRB-2']);
    expect(filterTickets(list, 'all', 'vahcm2').map(t => t.id)).toEqual(['TRB-2']);
    expect(filterTickets(list, 'all', 'müller').map(t => t.id)).toEqual(['TRB-2']);
    expect(filterTickets(list, 'all', '  ')).toHaveLength(2);
  });
});

describe('tiêu đề theo loại yêu cầu', () => {
  it('sự cố: loại sự cố + mã vận đơn; API cũ không có kind vẫn là sự cố', () => {
    const t = ticket({});
    expect(kindOf(t)).toBe('su_co');
    expect([ticketTitle(t), ticketSubline(t)]).toEqual(['Giao chậm / trễ hẹn', 'VAHCM1']);
  });

  it('hỗ trợ: tiêu đề + chủ đề; góp ý: "Góp ý", không dòng phụ', () => {
    const support = ticket({ kind: 'ho_tro', bill: '', type: 'Báo lỗi phần mềm', subject: 'Không in được nhãn' });
    expect([ticketTitle(support), ticketSubline(support)]).toEqual(['Không in được nhãn', 'Báo lỗi phần mềm']);
    const feedback = ticket({ kind: 'gop_y', bill: '', type: 'Góp ý' });
    expect([ticketTitle(feedback), ticketSubline(feedback)]).toEqual(['Góp ý', '']);
  });

  it('tìm được theo tiêu đề yêu cầu hỗ trợ', () => {
    const list = [ticket({ id: 'TRB-5', kind: 'ho_tro', bill: '', subject: 'Không in được nhãn' })];
    expect(filterTickets(list, 'all', 'in được').map(t => t.id)).toEqual(['TRB-5']);
  });
});
