import { describe, expect, it } from 'vitest';
import { filterInbox, groupByDay, noticeKind, troubleIdOf, type InboxItem } from './inbox';

const n = (over: Partial<InboxItem>): InboxItem => ({ imp: false, unread: false, title: 'Lịch cut-off', date: '13/10/2026 10:04', body: 'Nội dung', ...over });

describe('noticeKind / troubleIdOf', () => {
  it('nhận ra thông báo sự cố tự động, quan trọng, thường', () => {
    expect(noticeKind(n({ title: 'Sự cố TRB-2: Đã xử lý' }))).toBe('trouble');
    expect(noticeKind(n({ title: 'phụ phí xăng dầu', imp: true }))).toBe('important');
    expect(noticeKind(n({}))).toBe('general');
    expect(troubleIdOf('Sự cố trb-12: Đang xử lý')).toBe('TRB-12');
    expect(troubleIdOf('Lịch nghỉ lễ')).toBeNull();
  });
});

describe('filterInbox', () => {
  const items = [
    n({ title: 'Sự cố TRB-1: Đã xử lý', unread: true }),
    n({ title: 'Phụ phí xăng dầu', imp: true, body: 'xăng dầu tháng 11' }),
    n({ title: 'Bảo trì hệ thống' })
  ];

  it('lọc chưa đọc / quan trọng / sự cố', () => {
    expect(filterInbox(items, 'unread', '').map(x => x.title)).toEqual(['Sự cố TRB-1: Đã xử lý']);
    expect(filterInbox(items, 'important', '').map(x => x.title)).toEqual(['Phụ phí xăng dầu']);
    expect(filterInbox(items, 'trouble', '')).toHaveLength(1);
    expect(filterInbox(items, 'all', '')).toHaveLength(3);
  });

  it('tìm không dấu trong tiêu đề và nội dung', () => {
    expect(filterInbox(items, 'all', 'xang dau').map(x => x.title)).toEqual(['Phụ phí xăng dầu']);
    expect(filterInbox(items, 'all', 'bao tri')).toHaveLength(1);
  });
});

describe('groupByDay', () => {
  it('nhóm Hôm nay / Hôm qua / theo ngày, giữ thứ tự', () => {
    const groups = groupByDay(
      [n({ date: '13/10/2026 10:17' }), n({ date: '13/10/2026 10:04' }), n({ date: '12/10/2026 17:22' }), n({ date: '01/10/2026 09:00' })],
      new Date(2026, 9, 13)
    );
    expect(groups.map(g => [g.label, g.items.length])).toEqual([['Hôm nay', 2], ['Hôm qua', 1], ['01/10/2026', 1]]);
  });
});
