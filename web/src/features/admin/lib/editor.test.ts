import { describe, expect, it } from 'vitest';
import type { AdminNotification } from '../api';
import { copyDraft, draftFrom, emailLocked, emptyDraft, emptyItem, fitWidth, move, parseCodes, recipientsOf, summaryOf, toRequest, validateDraft, type Draft } from './editor';

const NOW = new Date(2026, 9, 24, 10, 0);
const item = (over: Partial<ReturnType<typeof emptyItem>>) => ({ ...emptyItem(), ...over });
const draft = (over: Partial<Draft>): Draft => ({ ...emptyDraft(NOW), title: 'Bản tin', items: [item({ body: 'Nội dung' })], ...over });
const baseNotice: AdminNotification = {
  id: '7', title: 'Phụ phí', status: 'scheduled', important: true, pinned: false, publishAt: '2026-11-01T07:00', expiresOn: '2026-11-30',
  createdAt: '24/10/2026 08:00', createdBy: 'admin', audience: 'type', customerTypes: [3, 4], customerCodes: [], recipients: 68,
  readCustomers: 0, readCount: 0, emailStatus: 'scheduled', emailRecipients: 0, emailSent: 0, items: [{ title: null, body: 'x', images: ['a.png'], link: 'https://a.vn' }]
};

describe('validateDraft', () => {
  it('cần tiêu đề; nháp không cần nội dung', () => {
    expect(validateDraft(draft({ title: ' ' }), false, NOW)).toBe('Nhập tiêu đề thông báo');
    expect(validateDraft(draft({ items: [emptyItem()] }), false, NOW)).toBeNull();
    expect(validateDraft(draft({ items: [emptyItem()] }), true, NOW)).toBe('Viết nội dung cho ít nhất 1 tin');
  });

  it('nhiều tin cần tiêu đề từng tin, link phải http(s)', () => {
    expect(validateDraft(draft({ items: [item({ title: 'A', body: 'x' }), item({ body: 'y' })] }), true, NOW)).toBe('Tin 2: nhập tiêu đề tin');
    expect(validateDraft(draft({ items: [item({ body: 'x', action: 'link', link: 'javascript:alert(1)' })] }), true, NOW))
      .toBe('Tin 1: link phải bắt đầu bằng http:// hoặc https://');
    expect(validateDraft(draft({ items: [item({ body: 'x', action: 'link', link: 'https://vietanexpress.com.vn' })] }), true, NOW)).toBeNull();
  });

  it('đối tượng nhận và giờ hẹn', () => {
    expect(validateDraft(draft({ audience: 'type' }), true, NOW)).toBe('Chọn ít nhất 1 loại khách');
    expect(validateDraft(draft({ audience: 'pick', customerCodes: ' , ' }), true, NOW)).toBe('Nhập mã khách nhận thông báo');
    expect(validateDraft(draft({ schedule: true, publishAt: '2026-10-24T09:00' }), true, NOW)).toBe('Giờ hẹn đăng phải sau thời điểm hiện tại');
    expect(validateDraft(draft({ schedule: true, publishAt: '2026-10-26T07:00', expiresOn: '2026-10-25' }), true, NOW)).toBe('Ngày hết hiện phải sau giờ đăng');
    expect(validateDraft(draft({ schedule: true, publishAt: '2026-10-26T07:00', expiresOn: '2026-10-26' }), true, NOW)).toBeNull();
  });
});

describe('toRequest', () => {
  it('bỏ tin trống, link chỉ gửi khi đã thêm, đối tượng theo lựa chọn', () => {
    const body = toRequest(
      draft({
        items: [item({ title: ' Tin A ', body: ' a ', images: ['1.png'], action: 'link', link: ' https://a.vn ' }), item({ body: 'b', link: 'https://bo.vn' }), emptyItem()],
        audience: 'pick',
        customerCodes: 'KH1, kh1\nKH2',
        customerTypes: [3],
        schedule: false
      }),
      true
    );
    expect(body.items).toEqual([
      { title: 'Tin A', body: 'a', images: ['1.png'], link: 'https://a.vn', detail: null },
      { title: null, body: 'b', images: [], link: null, detail: null }
    ]);
    expect([body.customerCodes, body.customerTypes, body.publishAt, body.expiresOn, body.publish]).toEqual([['KH1', 'KH2'], [], null, null, true]);
  });
});

describe('nội dung có định dạng và nút Xem chi tiết', () => {
  it('nội dung HTML được lọc, chỉ có chữ thì gửi chữ thường', () => {
    const body = toRequest(draft({ items: [item({ body: 'Tăng <b>2%</b><script>x</script>' }), item({ title: 'B', body: 'a &lt; b' })] }), true);
    expect(body.items.map(i => i.body)).toEqual(['Tăng <b>2%</b>', 'a < b']);
  });

  it('nút mở nội dung chi tiết cần có nội dung; chỉ gửi detail khi chọn nội dung chi tiết', () => {
    expect(validateDraft(draft({ items: [item({ body: 'x', action: 'detail', detail: '<p><br></p>' })] }), true, NOW))
      .toBe('Tin 1: soạn nội dung chi tiết hoặc bỏ nút Xem chi tiết');
    const body = toRequest(draft({ items: [item({ body: 'x', action: 'detail', detail: '<p>Bảng <u>giá</u></p>', link: 'https://bo.vn' })] }), true);
    expect(body.items[0]).toMatchObject({ link: null, detail: '<p>Bảng <u>giá</u></p>' });
    expect(summaryOf(draft({ items: [item({ body: 'x', action: 'detail', detail: 'chi tiết' })] })).details).toBe(1);
  });

  it('nội dung trống chỉ có thẻ định dạng thì báo nhập nội dung', () => {
    expect(validateDraft(draft({ items: [item({ body: '<p><b> </b></p>', images: ['a.png'] })] }), true, NOW)).toBe('Tin 1: nhập nội dung');
  });

  it('mở lại thông báo có nội dung chi tiết, chữ thường cũ giữ xuống dòng', () => {
    const d = draftFrom({ ...baseNotice, items: [{ title: 'A', body: 'dòng 1\ndòng 2', images: [], link: null, detail: '<p>ct</p>' }] }, NOW);
    expect(d.items[0]).toMatchObject({ body: 'dòng 1<br>dòng 2', action: 'detail', detail: '<p>ct</p>' });
  });
});

describe('draftFrom / emailLocked', () => {
  const n = baseNotice;

  it('mở lại thông báo hẹn giờ giữ giờ hẹn, link, loại khách', () => {
    const d = draftFrom(n, NOW);
    expect([d.schedule, d.publishAt, d.expiresOn, d.sendEmail, d.customerTypes]).toEqual([true, '2026-11-01T07:00', '2026-11-30', true, [3, 4]]);
    expect(d.items[0]).toMatchObject({ body: 'x', images: ['a.png'], link: 'https://a.vn', action: 'link' });
  });

  it('nhân bản: giữ nội dung và đối tượng, bỏ id / hẹn giờ / hạn hiện / email', () => {
    const d = copyDraft({ ...n, status: 'live', emailStatus: 'sent' }, NOW);
    expect([d.id, d.title, d.schedule, d.expiresOn, d.sendEmail]).toEqual([null, 'Phụ phí (bản sao)', false, '', false]);
    expect([d.important, d.audience, d.customerTypes]).toEqual([true, 'type', [3, 4]]);
    expect(d.items[0]).toMatchObject({ images: ['a.png'], link: 'https://a.vn' });
    expect(d.publishAt).toBe(emptyDraft(NOW).publishAt);
    expect(copyDraft({ ...n, title: 'x'.repeat(200) }, NOW).title).toHaveLength(200);
  });

  it('email đã gửi thì khóa lựa chọn gửi email', () => {
    expect(emailLocked(n)).toBe(false);
    expect(emailLocked({ ...n, emailStatus: 'sent' })).toBe(true);
    expect(emailLocked(null)).toBe(false);
  });
});

describe('tiện ích', () => {
  it('parseCodes bỏ trùng không phân biệt hoa thường', () => {
    expect(parseCodes('KH1, kh1;KH2\n KH3 ')).toEqual(['KH1', 'KH2', 'KH3']);
  });

  it('recipientsOf / summaryOf', () => {
    const audience = { total: 312, types: [{ id: 3, name: 'Loại 3', count: 51 }, { id: 4, name: 'Loại 4', count: 17 }] };
    expect(recipientsOf(draft({}), audience)).toBe(312);
    expect(recipientsOf(draft({ audience: 'type', customerTypes: [3, 4] }), audience)).toBe(68);
    expect(recipientsOf(draft({ audience: 'pick', customerCodes: 'A, B' }), audience)).toBe(2);
    expect(summaryOf(draft({ items: [item({ body: 'a', images: ['1', '2'], action: 'link', link: 'https://a.vn' }), emptyItem()] })))
      .toEqual({ items: 1, images: 2, links: 1, details: 0 });
  });

  it('move đổi chỗ, ngoài phạm vi giữ nguyên', () => {
    expect(move(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
    expect(move(['a', 'b'], 0, -1)).toEqual(['a', 'b']);
  });

  it('fitWidth thu nhỏ giữ tỉ lệ, không phóng to', () => {
    expect(fitWidth(3200, 1800, 1600)).toEqual({ width: 1600, height: 900 });
    expect(fitWidth(800, 600, 1600)).toEqual({ width: 800, height: 600 });
  });
});
