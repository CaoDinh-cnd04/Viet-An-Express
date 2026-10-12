import { describe, expect, it } from 'vitest';
import { autosaveKey, draftAutosaveKey } from './useOrderPrefill';

describe('autosaveKey', () => {
  it('khóa riêng theo tài khoản — admin và nhân viên cùng công ty không dùng chung bản tự lưu', () => {
    const admin = autosaveKey({ customerCode: 'SGB', userName: 'admin' });
    const staff = autosaveKey({ customerCode: 'SGB', userName: 'kho.hn' });
    expect(admin).not.toBe(staff);
    expect(admin).toBe('va.createOrder.autosave:SGB:admin');
  });

  it('chưa có phiên (backend chưa bật đăng nhập) dùng khóa chung', () => {
    expect(autosaveKey(null)).toBe('va.createOrder.autosave');
  });

  it('mỗi đơn nháp có bản tự lưu riêng, không lẫn với đơn mới', () => {
    const base = autosaveKey({ customerCode: 'SGB', userName: 'admin' });
    expect(draftAutosaveKey(base, 'a1')).toBe('va.createOrder.autosave:SGB:admin:draft:a1');
    expect(draftAutosaveKey(base, 'a1')).not.toBe(draftAutosaveKey(base, 'b2'));
  });
});
