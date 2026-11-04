import { describe, expect, it } from 'vitest';
import { createOrderSchema, defaultValues } from './schema';

const issueAt = (path: string, v = defaultValues()) =>
  createOrderSchema.safeParse(v).error?.issues.find(i => i.path.join('.') === path)?.message;

describe('createOrderSchema — dịch vụ', () => {
  it('đổi sang hãng nhiều hub mà chưa chọn hub thì báo lỗi Hub', () => {
    const v = defaultValues();
    v.service = { carrier: 'Fedex', hub: '', reference: '' };
    expect(issueAt('service.hub', v)).toBe('Chọn hub');
  });

  it('đã chọn hub thì không báo', () => {
    const v = defaultValues();
    v.service = { carrier: 'Chuyên tuyến', hub: 'Chuyên tuyến - Singapore', reference: '' };
    expect(issueAt('service.hub', v)).toBeUndefined();
  });

  it('đơn mới chưa có chi nhánh / dịch vụ — khách tự chọn (dịch vụ chọn sau nước đến)', () => {
    expect(issueAt('shipper.branch')).toBe('Chọn chi nhánh gửi');
    expect(issueAt('service.carrier')).toBe('Chọn dịch vụ');
  });
});

describe('createOrderSchema — tỉnh / bang', () => {
  it('Mỹ, Canada, Úc bắt buộc tỉnh / bang; nước khác thì không', () => {
    const v = defaultValues();
    v.receiver = { ...v.receiver, country: 'Australia', countryCode: 'AU' };
    expect(issueAt('receiver.state', v)).toBe('Nhập tỉnh / bang (bắt buộc với Mỹ, Canada, Úc)');
    v.receiver.state = 'NSW';
    expect(issueAt('receiver.state', v)).toBeUndefined();
    v.receiver = { ...v.receiver, country: 'Singapore', countryCode: 'SG', state: '' };
    expect(issueAt('receiver.state', v)).toBeUndefined();
  });
});

