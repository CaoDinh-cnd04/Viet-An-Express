import { describe, expect, it } from 'vitest';
import type { SupportTicket } from '../types';
import { canAnswer, historyOf } from './history';

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

  it('chỉ trả lời được khi CS đang chờ khách', () => {
    expect(canAnswer({ status: 'waitc' })).toBe(true);
    expect(canAnswer({ status: 'doing' })).toBe(false);
    expect(canAnswer({ status: 'done' })).toBe(false);
  });
});
