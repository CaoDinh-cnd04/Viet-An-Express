import { describe, expect, it } from 'vitest';
import type { TroubleTicket } from '../types';
import { filterTickets, lastActivity } from './chat';

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

describe('lastActivity', () => {
  it('chưa có trao đổi thì lấy mô tả ban đầu', () => {
    expect(lastActivity(ticket({}))).toEqual({ from: 'customer', text: 'Hàng chưa tới', date: '10/10/2026 09:00' });
  });

  it('lấy tin cuối có nội dung (bỏ lượt chỉ đổi trạng thái)', () => {
    const t = ticket({
      messages: [
        { from: 'vietan', author: 'CS', message: 'Đang kiểm tra', date: '11/10/2026 08:00' },
        { from: 'vietan', author: 'CS', message: '', date: '11/10/2026 09:00', status: 'doing' }
      ]
    });
    expect(lastActivity(t)).toEqual({ from: 'vietan', text: 'Đang kiểm tra', date: '11/10/2026 08:00' });
  });
});
