import { describe, expect, it } from 'vitest';
import { customerTrackingQuery, customerTrackingUrl, isMyTrackingHost, legacyAssetUrl } from './link';

describe('customerTrackingUrl', () => {
  it('dựng link theo mã khách, số bill và mã REF', () => {
    expect(customerTrackingUrl('https://mytracking.top', 'SPVN', '6174086', 'PO-1')).toBe('https://mytracking.top/tracking/SPVN/6174086?ref=PO-1');
    expect(customerTrackingUrl('https://mytracking.top', 'SPVN', '6174086')).toBe('https://mytracking.top/tracking/SPVN/6174086');
  });

  it('thiếu số bill → link gốc của khách', () => {
    expect(customerTrackingUrl('https://mytracking.top', ' SPVN ')).toBe('https://mytracking.top/tracking/SPVN/');
  });

  it('mã có ký tự đặc biệt được mã hóa', () => {
    expect(customerTrackingUrl('https://mytracking.top', 'A B', '', 'x&y')).toBe('https://mytracking.top/tracking/A%20B/?ref=x%26y');
  });
});

describe('isMyTrackingHost', () => {
  it('so tên miền không phân biệt hoa thường', () => {
    expect(isMyTrackingHost('MyTracking.top', 'https://mytracking.top')).toBe(true);
    expect(isMyTrackingHost('beta.vietanexpress.com.vn', 'https://mytracking.top')).toBe(false);
    expect(isMyTrackingHost('mytracking.top', 'không phải url')).toBe(false);
  });
});

describe('customerTrackingQuery', () => {
  it('ưu tiên số bill trên đường dẫn, không có thì dùng mã REF', () => {
    expect(customerTrackingQuery('6174086', 'PO-1')).toEqual(['6174086']);
    expect(customerTrackingQuery('6174086, 6174087', null)).toEqual(['6174086', '6174087']);
    expect(customerTrackingQuery(undefined, ' po-1 ')).toEqual(['PO-1']);
    expect(customerTrackingQuery('abc,ABC', null)).toEqual(['ABC']);
    expect(customerTrackingQuery('', '')).toEqual([]);
  });
});

describe('legacyAssetUrl', () => {
  it('ghép đường dẫn tương đối của hệ thống cũ với nơi chứa ảnh', () => {
    expect(legacyAssetUrl('/Upload/Banner/a.jpg', 'https://mytracking.top')).toBe('https://mytracking.top/Upload/Banner/a.jpg');
    expect(legacyAssetUrl('https://cdn.x/a.png', 'https://mytracking.top')).toBe('https://cdn.x/a.png');
    expect(legacyAssetUrl('data:image/png;base64,AAA', 'https://mytracking.top')).toBe('data:image/png;base64,AAA');
    expect(legacyAssetUrl('//cdn.x/a.png', 'https://mytracking.top')).toBe('//cdn.x/a.png');
    expect(legacyAssetUrl('', 'https://mytracking.top')).toBe('');
  });
});

