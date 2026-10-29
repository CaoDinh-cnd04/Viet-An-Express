import { describe, expect, it } from 'vitest';
import { draftError, newDraft, toNewSupportRequest, type RequestDraft } from './draft';

const draft = (over: Partial<RequestDraft>): RequestDraft => ({
  kind: 'ho_tro', bill: '94012388', topic: 'Thất lạc kiện', rating: null, text: '', urgent: false, requester: 'Hà', contact: '0909', ...over
});

describe('form gửi yêu cầu → POST /troubles', () => {
  it('form trống theo link: sự cố cũ gộp vào hỗ trợ; góp ý không giữ mã vận đơn', () => {
    const contact = { requester: 'Hà', contact: '0909' };
    expect(newDraft({ kind: 'su_co', bill: '6179215' }, contact)).toMatchObject({ kind: 'ho_tro', bill: '6179215', topic: '' });
    expect(newDraft({ kind: 'gop_y', bill: '6179215' }, contact)).toMatchObject({ kind: 'gop_y', bill: '' });
    expect(newDraft({ topic: 'Sửa thông tin người gửi' }, contact)).toMatchObject({ kind: 'ho_tro', topic: 'Sửa thông tin người gửi', contact: '0909' });
  });

  it('có mã vận đơn thì nội dung không bắt buộc: tự ghi vấn đề + vận đơn; gấp → Rất gấp', () => {
    expect(draftError(draft({}))).toBeNull();
    expect(toNewSupportRequest(draft({ urgent: true }), null)).toMatchObject({
      kind: 'ho_tro', bill: '94012388', type: 'Thất lạc kiện', lv: 'high', desc: 'Thất lạc kiện — vận đơn 94012388', req: 'Hà', contact: '0909'
    });
    expect(toNewSupportRequest(draft({ text: 'Người nhận chỉ nhận 2/3 kiện' }), null).desc).toBe('Người nhận chỉ nhận 2/3 kiện');
  });

  it('hỗ trợ không gắn vận đơn: cần nội dung, không gửi bill, tiêu đề để backend lấy theo vấn đề', () => {
    const base = draft({ bill: '', topic: 'Tra cứu / tracking' });
    expect(draftError(base)).toBe('Viết nội dung cần hỗ trợ');
    const sent = toNewSupportRequest({ ...base, text: 'Lỗi' }, null);
    expect(sent).toMatchObject({ kind: 'ho_tro', bill: undefined, desc: 'Tra cứu / tracking. Lỗi', lv: 'low' });
    expect(sent.subject).toBeUndefined();
  });

  it('góp ý: mức hài lòng ghi đầu nội dung; không cần liên hệ', () => {
    const g = draft({ kind: 'gop_y', bill: '', topic: 'Góp ý nâng cấp', rating: 4, text: 'Muốn xuất Excel theo tháng', contact: '' });
    expect(draftError(g)).toBeNull();
    expect(toNewSupportRequest(g, null)).toMatchObject({ type: 'Góp ý nâng cấp', desc: 'Mức hài lòng: 4/5\nMuốn xuất Excel theo tháng' });
    expect(draftError({ ...g, topic: '' })).toBe('Chọn nội dung góp ý');
    expect(draftError({ ...g, text: ' ' })).toBe('Viết góp ý của bạn');
  });

  it('thiếu vấn đề / nội dung / liên hệ', () => {
    expect(draftError(draft({ bill: ' ' }))).toBe('Viết nội dung cần hỗ trợ');
    expect(draftError(draft({ topic: '' }))).toBe('Chọn vấn đề cần hỗ trợ');
    expect(draftError(draft({ contact: '' }))).toBe('Nhập SĐT hoặc email để CS liên hệ');
  });
});
