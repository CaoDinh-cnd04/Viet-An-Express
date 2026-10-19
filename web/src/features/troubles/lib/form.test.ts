import { describe, expect, it } from 'vitest';
import { toNewTrouble, troubleFormError, type TroubleFormValues } from './form';

const values = (over: Partial<TroubleFormValues> = {}): TroubleFormValues => ({
  bill: 'VAHCM6010844CA', type: 'Giao chậm / trễ hẹn', subject: '', lv: 'mid', desc: 'Người nhận chưa nhận được hàng.', req: 'An', contact: '0909123456', ...over
});

describe('form gửi yêu cầu', () => {
  it('sự cố cần vận đơn, vấn đề, mô tả đủ dài, người yêu cầu, liên hệ', () => {
    expect(troubleFormError('su_co', values())).toBeNull();
    expect(troubleFormError('su_co', values({ bill: ' ' }))).toBe('Nhập mã vận đơn');
    expect(troubleFormError('su_co', values({ desc: 'ngắn' }))).toBe('Nội dung tối thiểu 10 ký tự để CS xử lý nhanh');
    expect(troubleFormError('su_co', values({ req: '' }))).toBe('Nhập người yêu cầu');
  });

  it('hỗ trợ cần chủ đề + tiêu đề, không cần vận đơn; góp ý chỉ cần nội dung', () => {
    expect(troubleFormError('ho_tro', values({ bill: '', subject: 'Không in được nhãn' }))).toBeNull();
    expect(troubleFormError('ho_tro', values({ subject: 'abc' }))).toBe('Tiêu đề tối thiểu 5 ký tự');
    expect(troubleFormError('gop_y', values({ bill: '', type: '', req: '', contact: '' }))).toBeNull();
  });

  it('chỉ gửi trường của loại đó', () => {
    const support = toNewTrouble('ho_tro', values({ subject: ' Sửa địa chỉ ' }), null);
    expect(support).toMatchObject({ kind: 'ho_tro', bill: undefined, lv: undefined, subject: 'Sửa địa chỉ' });
    expect(toNewTrouble('gop_y', values(), null)).toMatchObject({ type: undefined, subject: undefined, bill: undefined });
  });
});
