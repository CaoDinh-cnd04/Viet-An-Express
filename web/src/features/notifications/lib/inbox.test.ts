import { describe, expect, it } from 'vitest';
import { bellItems, excerpt, filterCounts, filterInbox, groupByDay, imagesOf, partsOf, shortTime, ticketIdOf, type InboxItem } from './inbox';

const n = (over: Partial<InboxItem>): InboxItem => ({
  imp: false,
  pin: false,
  unread: false,
  kind: 'notice',
  title: 'Lịch cut-off',
  date: '13/10/2026 10:04',
  body: 'Nội dung',
  items: [],
  ...over
});

const part = (title: string | null, body: string, images: string[] = []) => ({ title, body, images, link: null });

describe('partsOf / excerpt / imagesOf', () => {
  it('thông báo cũ không có tin con → 1 tin từ body + link', () => {
    expect(partsOf(n({ body: 'Kho nghỉ', link: 'https://a.vn' }))).toEqual([{ title: null, body: 'Kho nghỉ', images: [], link: 'https://a.vn' }]);
    expect(excerpt(n({ body: 'Dòng 1\n\nDòng 2' }))).toBe('Dòng 1 Dòng 2');
  });

  it('nhiều tin → tóm tắt bằng tiêu đề các tin, gom ảnh mọi tin', () => {
    const multi = n({ items: [part('Phụ phí', 'a', ['1.png']), part('Nghỉ lễ', 'b', ['2.png', '3.png'])] });
    expect(excerpt(multi)).toBe('Phụ phí · Nghỉ lễ');
    expect(imagesOf(multi)).toEqual(['1.png', '2.png', '3.png']);
  });

  it('mã yêu cầu trong tiêu đề thông báo hỗ trợ', () => {
    expect(ticketIdOf('Sự cố trb-12: Đang xử lý')).toBe('TRB-12');
    expect(ticketIdOf('Lịch nghỉ lễ')).toBeNull();
  });
});

describe('filterInbox / filterCounts', () => {
  const items = [
    n({ title: 'Sự cố TRB-1: Đã xử lý', unread: true, kind: 'support' }),
    n({ title: 'Phụ phí xăng dầu', imp: true, unread: true, body: 'xăng dầu tháng 11' }),
    n({ title: 'Bản tin', items: [part('Bảo trì hệ thống', 'tối nay')] })
  ];

  it('Tất cả / Chưa đọc / Quan trọng chỉ gồm thông báo Việt An; Hỗ trợ & sự cố tách riêng', () => {
    expect(filterInbox(items, 'all', '').map(x => x.title)).toEqual(['Phụ phí xăng dầu', 'Bản tin']);
    expect(filterInbox(items, 'unread', '').map(x => x.title)).toEqual(['Phụ phí xăng dầu']);
    expect(filterInbox(items, 'important', '').map(x => x.title)).toEqual(['Phụ phí xăng dầu']);
    expect(filterInbox(items, 'support', '').map(x => x.title)).toEqual(['Sự cố TRB-1: Đã xử lý']);
    expect(filterCounts(items)).toEqual({ all: 2, unread: 1, important: 1, support: 1 });
  });

  it('tìm không dấu trong tiêu đề và nội dung các tin', () => {
    expect(filterInbox(items, 'all', 'xang dau').map(x => x.title)).toEqual(['Phụ phí xăng dầu']);
    expect(filterInbox(items, 'all', 'bao tri').map(x => x.title)).toEqual(['Bản tin']);
  });
});

describe('groupByDay / shortTime', () => {
  it('ghim lên nhóm "Đã ghim", còn lại Hôm nay / Hôm qua / theo ngày, giữ thứ tự', () => {
    const groups = groupByDay(
      [n({ pin: true, date: '01/09/2026 08:00' }), n({ date: '13/10/2026 10:17' }), n({ date: '13/10/2026 10:04' }), n({ date: '12/10/2026 17:22' }), n({ date: '01/10/2026 09:00' })],
      new Date(2026, 9, 13)
    );
    expect(groups.map(g => [g.label, g.items.length])).toEqual([['Đã ghim', 1], ['Hôm nay', 2], ['Hôm qua', 1], ['01/10/2026', 1]]);
  });

  it('hôm nay hiện giờ, ngày khác hiện ngày/tháng', () => {
    expect(shortTime('13/10/2026 10:17', new Date(2026, 9, 13))).toBe('10:17');
    expect(shortTime('12/10/2026 17:22', new Date(2026, 9, 13))).toBe('12/10');
  });
});

describe('bellItems', () => {
  it('tin chưa đọc lên trước, giữ thứ tự mới nhất; còn chỗ mới thêm tin đã đọc', () => {
    const list = [n({ title: 'đọc 1' }), n({ title: 'mới 1', unread: true }), n({ title: 'đọc 2' }), n({ title: 'mới 2', unread: true })];
    expect(bellItems(list, 3).map(x => x.title)).toEqual(['mới 1', 'mới 2', 'đọc 1']);
    expect(bellItems(list, 1).map(x => x.title)).toEqual(['mới 1']);
    expect(bellItems([], 6)).toEqual([]);
  });
});
