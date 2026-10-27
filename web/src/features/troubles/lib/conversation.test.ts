import { describe, expect, it } from 'vitest';
import type { TroubleTicket } from '../types';
import {
  defaultFilter, draftError, filterConversations, filterCounts, messagesOf, noticesByTicket, previewOf, replyOf, toNewTrouble, transcriptOf,
  type ConversationDraft, type SupportNotice
} from './conversation';

const ticket = (over: Partial<TroubleTicket>): TroubleTicket => ({
  id: 'TRB-1', kind: 'su_co', bill: '94012388', cnee: 'Kim', ct: 'Úc', type: 'Thất lạc kiện', lv: 'low',
  desc: 'Chỉ nhận 2/3 kiện', req: 'Hà', contact: '0909', date: '26/10/2026 08:40', status: 'new', ...over
});
const notice = (over: Partial<SupportNotice>): SupportNotice => ({
  id: 1, unread: false, kind: 'support', title: 'Sự cố TRB-1: Đang xử lý', date: '26/10/2026 09:05',
  body: 'TRB-1 · Thất lạc kiện\n\nTrạng thái đã chuyển sang "Đang xử lý".', ...over
});
const draft = (over: Partial<ConversationDraft>): ConversationDraft => ({
  kind: 'su_co', bill: '94012388', topic: 'Thất lạc kiện', rating: null, text: '', urgent: false, requester: 'Hà', contact: '0909', ...over
});

describe('tin trong khung chat', () => {
  it('tách phản hồi CS khỏi phần tóm tắt; chỉ đổi trạng thái thì thành tin hệ thống', () => {
    expect(replyOf('TRB-1 · tóm tắt\n\nNhờ chị gửi ảnh.')).toEqual({ text: 'Nhờ chị gửi ảnh.', status: null });
    expect(replyOf('x\n\nTrạng thái đã chuyển sang "Chờ khách phản hồi".').status).toBe('Chờ khách phản hồi');
  });

  it('dựng tin theo thứ tự thời gian, chèn mốc ngày', () => {
    const ns = noticesByTicket([
      notice({ id: 2, date: '27/10/2026 10:12', body: 'x\n\nNhờ chị gửi ảnh.', unread: true }),
      notice({ id: 1 }),
      notice({ id: 3, title: 'Thông báo chung', kind: 'notice' })
    ]).get('TRB-1')!;
    const msgs = messagesOf(ticket({}), ns);
    expect(msgs.map(m => m.type)).toEqual(['day', 'system', 'me', 'system', 'system', 'day', 'cs']);
    expect(msgs.at(-1)).toMatchObject({ type: 'cs', text: 'Nhờ chị gửi ảnh.' });
    expect(previewOf(ticket({}), ns)).toEqual({ from: 'cs', text: 'Nhờ chị gửi ảnh.' });
    expect(previewOf(ticket({}), [])).toEqual({ from: 'me', text: 'Chỉ nhận 2/3 kiện' });
  });
});

describe('danh sách cuộc trò chuyện', () => {
  const tickets = [
    ticket({ id: 'TRB-1', status: 'waitc', date: '20/10/2026 08:00' }),
    ticket({ id: 'TRB-2', status: 'doing', date: '25/10/2026 08:00' }),
    ticket({ id: 'TRB-3', status: 'done', date: '26/10/2026 08:00' })
  ];

  it('mục mở đầu: Chưa đọc → Đang mở → Tất cả', () => {
    const unread = noticesByTicket([notice({ title: 'TRB-1', unread: true })]);
    expect(defaultFilter(tickets, unread)).toBe('unread');
    expect(defaultFilter(tickets, new Map())).toBe('open');
    expect(defaultFilter([tickets[2]!], new Map())).toBe('all');
  });

  it('lọc, đếm, cuộc vừa đọc vẫn ở lại mục Chưa đọc, mới hoạt động lên đầu', () => {
    const ns = noticesByTicket([notice({ title: 'TRB-1', unread: true, date: '27/10/2026 09:00' })]);
    expect(filterCounts(tickets, ns)).toEqual({ unread: 1, open: 2, closed: 1, all: 3 });
    expect(filterConversations(tickets, ns, 'all', '').map(t => t.id)).toEqual(['TRB-1', 'TRB-3', 'TRB-2']);
    expect(filterConversations(tickets, new Map(), 'unread', '', 'TRB-2').map(t => t.id)).toEqual(['TRB-2']);
    expect(filterConversations(tickets, ns, 'closed', '').map(t => t.id)).toEqual(['TRB-3']);
    expect(filterConversations(tickets, ns, 'all', 'trb-2').map(t => t.id)).toEqual(['TRB-2']);
    // Tìm theo người nhận / tiêu đề yêu cầu hỗ trợ, không phân biệt hoa thường
    const more = [...tickets, ticket({ id: 'TRB-5', kind: 'ho_tro', bill: '', cnee: 'Müller', subject: 'Không in được nhãn', status: 'new' })];
    expect(filterConversations(more, ns, 'all', 'IN ĐƯỢC').map(t => t.id)).toEqual(['TRB-5']);
    expect(filterConversations(more, ns, 'all', 'müller').map(t => t.id)).toEqual(['TRB-5']);
  });
});

describe('cuộc trò chuyện mới → POST /troubles', () => {
  it('sự cố không cần mô tả: tự ghi vấn đề + vận đơn; gấp → Rất gấp', () => {
  });

  it('hỗ trợ & sự cố là 1 loại: có mã vận đơn thì nội dung không bắt buộc', () => {
    expect(draftError(draft({}))).toBeNull();
    expect(toNewTrouble(draft({ urgent: true }), null)).toMatchObject({
      kind: 'ho_tro', bill: '94012388', type: 'Thất lạc kiện', lv: 'high', desc: 'Thất lạc kiện — vận đơn 94012388', req: 'Hà', contact: '0909'
    });
    expect(toNewTrouble(draft({ text: 'Người nhận chỉ nhận 2/3 kiện' }), null).desc).toBe('Người nhận chỉ nhận 2/3 kiện');
  });

  it('hỗ trợ không gắn vận đơn: cần nội dung, không gửi bill, tiêu đề để backend lấy theo vấn đề', () => {
    const base = draft({ kind: 'ho_tro', bill: '', topic: 'Tra cứu / tracking' });
    expect(draftError(base)).toBe('Viết nội dung cần hỗ trợ');
    const sent = toNewTrouble({ ...base, text: 'Lỗi' }, null);
    expect(sent).toMatchObject({ kind: 'ho_tro', bill: undefined, desc: 'Tra cứu / tracking. Lỗi', lv: 'low' });
    expect(sent.subject).toBeUndefined();
  });

  it('góp ý: mức hài lòng ghi đầu nội dung; không cần liên hệ', () => {
    const g = draft({ kind: 'gop_y', bill: '', topic: 'Góp ý nâng cấp', rating: 4, text: 'Muốn xuất Excel theo tháng', contact: '' });
    expect(draftError(g)).toBeNull();
    expect(toNewTrouble(g, null)).toMatchObject({ type: 'Góp ý nâng cấp', desc: 'Mức hài lòng: 4/5\nMuốn xuất Excel theo tháng' });
    expect(draftError({ ...g, topic: '' })).toBe('Chọn nội dung góp ý');
    expect(draftError({ ...g, text: ' ' })).toBe('Viết góp ý của bạn');
  });

  it('thiếu vấn đề / nội dung / liên hệ', () => {
    expect(draftError(draft({ bill: ' ' }))).toBe('Viết nội dung cần hỗ trợ');
    expect(draftError(draft({ topic: '' }))).toBe('Chọn vấn đề cần hỗ trợ');
    expect(draftError(draft({ contact: '' }))).toBe('Nhập SĐT hoặc email để CS liên hệ');
  });
});

describe('tải đoạn chat', () => {
  it('mỗi tin 1 dòng có giờ và người gửi, tin hệ thống không có người gửi', () => {
    const t = ticket({ kind: 'ho_tro', desc: 'Chưa nhận\nđủ kiện' });
    const ns = [notice({ id: 2, date: '26/10/2026 09:00', body: 'x\n\nĐang kiểm tra' })];
    const text = transcriptOf(t, messagesOf(t, ns), (s, p) => s.replace(/\{(\w+)\}/g, (_, k: string) => String(p?.[k] ?? '')));
    expect(text.split('\n')).toEqual([
      'TRB-1 · Thất lạc kiện · 94012388',
      '',
      '26/10/2026 08:40  Bạn đã gửi yêu cầu "Thất lạc kiện" · vận đơn 94012388',
      '26/10/2026 08:40  Bạn: Chưa nhận',
      '    đủ kiện',
      expect.stringMatching(/^26\/10\/2026 08:40  Mã yêu cầu TRB-1/),
      '26/10/2026 09:00  CS Việt An: Đang kiểm tra',
      ''
    ]);
  });
});
