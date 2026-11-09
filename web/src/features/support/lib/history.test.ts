import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '../types';
import { historyOf, lastMessage, unansweredCount } from './history';

const ticket = (over: Partial<SupportTicket>) => ({ id: 'TRB-12', date: '26/10/2026 08:40', status: 'new', ...over }) as SupportTicket;

describe('lịch sử yêu cầu', () => {
  it('mới nhất trước: các lần trả lời qua lại, cuối cùng là lúc khách gửi', () => {
    const history = historyOf(ticket({
      thread: [
        { from: 'cs', at: '27/10/2026 10:15', text: 'Nhờ chị gửi ảnh kiện.' },
        { from: 'customer', at: '27/10/2026 11:00', text: 'Em gửi qua email rồi ạ.' }
      ]
    }));
    expect(history).toEqual([
      { at: '27/10/2026 11:00', kind: 'customer', text: 'Em gửi qua email rồi ạ.' },
      { at: '27/10/2026 10:15', kind: 'cs', text: 'Nhờ chị gửi ảnh kiện.' },
      { at: '26/10/2026 08:40', kind: 'sent' }
    ]);
  });

  it('chưa trả lời (hoặc backend cũ không có thread): chỉ có lúc gửi', () => {
    expect(historyOf(ticket({}))).toEqual([{ at: '26/10/2026 08:40', kind: 'sent' }]);
  });

  it('tin mới = các tin cuối của bên kia người xem chưa trả lời; đã xử lý thì không đánh dấu', () => {
    const t = ticket({ status: 'doing', thread: [
      { from: 'cs', at: '27/10/2026 10:15', text: 'Nhờ chị gửi ảnh kiện.' },
      { from: 'customer', at: '27/10/2026 10:20', text: 'Dạ' },
      { from: 'customer', at: '27/10/2026 10:21', text: 'Em gửi rồi' }
    ] });
    expect(unansweredCount(t, 'cs')).toBe(2);
    expect(unansweredCount(t, 'customer')).toBe(0);
    expect(unansweredCount(ticket({ status: 'new' }), 'cs')).toBe(1); // yêu cầu mới chưa ai trả lời
    expect(unansweredCount(ticket({ status: 'new' }), 'customer')).toBe(0);
    expect(unansweredCount({ ...t, status: 'done' }, 'cs')).toBe(0);
  });

  it('tin gần nhất trên thẻ: lần trả lời cuối, chưa ai trả lời thì nội dung khách gửi', () => {
    expect(lastMessage(ticket({ desc: 'Hàng chưa tới', thread: [{ from: 'cs', at: '27/10/2026 10:15', text: 'Nhờ chị gửi ảnh kiện.' }] })))
      .toEqual({ from: 'cs', text: 'Nhờ chị gửi ảnh kiện.' });
    expect(lastMessage(ticket({ desc: 'Hàng chưa tới' }))).toEqual({ from: 'customer', text: 'Hàng chưa tới' });
  });
});
