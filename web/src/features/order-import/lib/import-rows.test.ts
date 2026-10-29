import { describe, expect, it } from 'vitest';
import type { ImportRow } from '../types';
import { buildImportForm, checkImportFile, formatKg, rowState, serviceHubError, sortRows } from './import-rows';

const row = (over: Partial<ImportRow> = {}): ImportRow => ({
  line: 3, ref: '', type: 'PACK', consignee: 'ACME', countryCode: 'US', country: 'United States', city: 'LA',
  pieces: 1, weightKg: 5, chargeableKg: 7.2, value: 100, currency: 'USD', products: 1, errors: [], warnings: [], ...over
});

describe('checkImportFile', () => {
  it('nhận .xlsx', () => expect(checkImportFile({ name: 'Mau_Excel_Tao_Don.XLSX', size: 20_000 })).toBeNull());
  it('từ chối CSV, .xls và file quá lớn', () => {
    expect(checkImportFile({ name: 'don.csv', size: 10 })).toMatch(/\.xlsx/);
    expect(checkImportFile({ name: 'don.xls', size: 10 })).toMatch(/\.xlsx/);
    expect(checkImportFile({ name: 'don.xlsx', size: 6 * 1024 * 1024 })).toMatch(/quá lớn/);
  });
});

describe('serviceHubError', () => {
  it('không chọn dịch vụ thì không cần hub', () => expect(serviceHubError({ service: '', hub: '' })).toBeNull());
  it('đã chọn dịch vụ thì bắt buộc hub', () => {
    expect(serviceHubError({ service: 'DHL', hub: '' })).toMatch(/Hub/);
    expect(serviceHubError({ service: 'DHL', hub: 'DHL - Singapore' })).toBeNull();
  });
});

describe('buildImportForm', () => {
  const file = new File(['x'], 'a.xlsx');
  it('gửi dịch vụ + hub khi có chọn', () => {
    const f = buildImportForm(file, { service: 'DHL', hub: 'DHL - VN', branch: 'TP.HCM' });
    expect(f.get('file')).toBeInstanceOf(File);
    expect([f.get('service'), f.get('hub'), f.get('branch')]).toEqual(['DHL', 'DHL - VN', 'TP.HCM']);
  });
  it('không chọn dịch vụ thì không gửi service / hub (backend lấy theo file)', () => {
    const f = buildImportForm(file, { service: '', hub: 'DHL - VN', branch: '' });
    expect(f.has('service')).toBe(false);
    expect(f.has('hub')).toBe(false);
    expect(f.has('branch')).toBe(false);
  });
});

describe('rowState & sortRows', () => {
  it('phân loại dòng', () => {
    expect(rowState(row({ bill: '90000001' }))).toBe('created');
    expect(rowState(row({ errors: ['Thiếu Add1'] }))).toBe('error');
    expect(rowState(row({ warnings: ['Ref trùng'] }))).toBe('warning');
    expect(rowState(row())).toBe('ok');
  });
  it('dòng lỗi lên đầu, còn lại theo thứ tự dòng', () => {
    const sorted = sortRows([row({ line: 3 }), row({ line: 5, errors: ['x'] }), row({ line: 4, warnings: ['y'] })]);
    expect(sorted.map(r => r.line)).toEqual([5, 4, 3]);
  });
});

describe('định dạng', () => {
  it('cân', () => {
    expect(formatKg(7.2)).toBe('7.2 kg');
    expect(formatKg(16)).toBe('16 kg');
  });
});
