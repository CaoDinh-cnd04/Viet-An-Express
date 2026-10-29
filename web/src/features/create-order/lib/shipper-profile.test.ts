import { describe, expect, it } from 'vitest';
import { defaultValues } from '@/features/order-form';
import { shipperFromProfile } from './shipper-profile';

const profile = {
  companyName: 'SCS CO., LTD',
  contactName: ' MR TUẤN ANH ',
  phone: '0909 123 456',
  address: '14 Sam Sơn, Tân Sơn Nhất, TP.HCM',
  taxCode: '0312345678',
  email: 'cs@scs.vn'
};

describe('shipperFromProfile', () => {
  it('đơn mới: điền sẵn mọi trường có trong hồ sơ khách', () => {
    expect(shipperFromProfile(defaultValues().shipper, profile)).toEqual({
      company: 'SCS CO., LTD',
      contact: 'MR TUẤN ANH',
      tel: '0909 123 456',
      address: '14 Sam Sơn, Tân Sơn Nhất, TP.HCM',
      taxId: '0312345678',
      email: 'cs@scs.vn'
    });
  });

  it('không ghi đè ô đã có dữ liệu (nhân bản đơn, mở lại nháp)', () => {
    const current = { ...defaultValues().shipper, company: 'CÔNG TY KHÁC', contact: '  ', tel: '0988000111' };
    const fill = shipperFromProfile(current, profile);
    expect(fill).not.toHaveProperty('company');
    expect(fill).not.toHaveProperty('tel');
    expect(fill.contact).toBe('MR TUẤN ANH');
  });

  it('bỏ qua dữ liệu cũ không hợp lệ, cắt địa chỉ theo giới hạn', () => {
    const fill = shipperFromProfile(defaultValues().shipper, {
      companyName: 'SCS',
      contactName: null,
      phone: 'anh Tuấn',
      email: 'khong-phai-email',
      address: 'A'.repeat(80)
    });
    expect(fill).toEqual({ company: 'SCS', address: 'A'.repeat(60) });
  });
});
