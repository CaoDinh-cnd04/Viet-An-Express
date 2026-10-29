import { describe, expect, it } from 'vitest';
import { EU_COUNTRIES, euCountryCode, isEuCountry } from '@/shared/config/eu';
import { validateEoriNo, validateIossNo } from './eu-tax';
import { createOrderSchema, defaultValues } from '../schema';

describe('EU receiver tax identifiers', () => {
  it('nhận đúng 27 nước EU, không gồm GB, NO, CH', () => {
    expect(EU_COUNTRIES.size).toBe(27);
    expect(isEuCountry(' de ')).toBe(true);
    for (const code of ['GB', 'NO', 'CH', 'US', '']) expect(isEuCountry(code)).toBe(false);
    expect(euCountryCode('Germany')).toBe('DE');
  });
  it('hai mã là tùy chọn và kiểm tra định dạng khi nhập ở EU', () => {
    expect(validateIossNo('DE', '')).toBe(true);
    expect(validateEoriNo('DE', '')).toBe(true);
    expect(validateIossNo('FR', 'IM1234567890')).toBe(true);
    expect(validateEoriNo('DE', 'DE123456789012345')).toBe(true);
    for (const value of ['IM123', 'im1234567890', 'IM12345678901']) expect(validateIossNo('DE', value)).toBe(false);
    for (const value of ['DE', 'de123', 'DE1234567890123456', 'DE12-34']) expect(validateEoriNo('DE', value)).toBe(false);
  });
  it('không validate dữ liệu ẩn ngoài EU', () => {
    expect(validateIossNo('GB', 'invalid')).toBe(true);
    expect(validateEoriNo('US', 'invalid')).toBe(true);
    const values = defaultValues();
    values.receiver.country = 'United States';
    values.receiver.countryCode = 'DE';
    values.receiver.iossNo = 'invalid';
    values.receiver.eoriNo = 'invalid';
    const result = createOrderSchema.safeParse(values);
    if (!result.success) expect(result.error.issues.some(i => ['receiver.iossNo', 'receiver.eoriNo'].includes(i.path.join('.')))).toBe(false);
  });
  it('schema trả lỗi đúng field cho DOC và nháp cũ dùng tên nước', () => {
    const values = defaultValues();
    values.shipment.type = 'DOC';
    values.receiver.country = 'Germany';
    values.receiver.iossNo = 'invalid';
    values.receiver.eoriNo = 'invalid';
    const result = createOrderSchema.safeParse(values);
    expect(result.success).toBe(false);
    if (!result.success) {
      const paths = result.error.issues.map(i => i.path.join('.'));
      expect(paths).toContain('receiver.iossNo');
      expect(paths).toContain('receiver.eoriNo');
    }
  });
});
