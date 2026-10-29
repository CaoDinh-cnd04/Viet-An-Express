import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '../types';
import { kindOf, ticketSubline, ticketTitle } from './ticket';

const ticket = (over: Partial<SupportTicket>): SupportTicket => ({
  id: 'TRB-1', bill: 'VAHCM1', cnee: 'John Doe', ct: 'US', type: 'Giao chậm / trễ hẹn', lv: 'low', desc: 'Hàng chưa tới',
  req: 'An', contact: '', date: '10/10/2026 09:00', status: 'new', ...over
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
});
