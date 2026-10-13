import { describe, expect, it } from 'vitest';
import { searchCountries, searchHs } from './quick-lookup';

const sources = [
  {
    source: 'Thực phẩm khô',
    items: [
      { hs: '0813.40', en: 'Dried mango', vi: 'Xoài sấy dẻo' },
      { hs: '0906.19', en: 'Cinnamon sticks', vi: 'Quế cây' }
    ]
  },
  {
    source: 'Thư viện mặt hàng',
    items: [
      { hs: '081340', en: 'Dried Mango', vi: 'Xoài sấy' },
      { hs: '6204.43', en: 'Women dress', vi: 'Đầm nữ' },
      { hs: '', en: 'No code', vi: 'Không mã' }
    ]
  }
];

describe('searchHs', () => {
  it('tìm theo tên tiếng Việt không dấu và tiếng Anh, bỏ trùng HS + tên', () => {
    expect(searchHs('xoai', sources).map(h => h.hs)).toEqual(['0813.40']);
    expect(searchHs('dress', sources).map(h => h.vi)).toEqual(['Đầm nữ']);
    expect(searchHs('dam nu', sources).map(h => h.hs)).toEqual(['6204.43']);
  });

  it('gõ số HS (có hoặc không dấu chấm) thì khớp đầu mã', () => {
    expect(searchHs('0906', sources).map(h => h.en)).toEqual(['Cinnamon sticks']);
    expect(searchHs('6204.4', sources).map(h => h.en)).toEqual(['Women dress']);
  });

  it('bỏ qua mặt hàng chưa có HS và từ khóa quá ngắn', () => {
    expect(searchHs('no code', sources)).toEqual([]);
    expect(searchHs('x', sources)).toEqual([]);
  });
});

describe('searchCountries', () => {
  const countries = [
    { code: 'US', name: 'United States', dialCode: '+1' },
    { code: 'VN', name: 'Viet Nam', dialCode: '+84' },
    { code: 'AU', name: 'Australia', dialCode: '+61' },
    { code: 'AT', name: 'Austria', dialCode: '+43' }
  ];

  it('khớp mã 2 chữ, mã điện thoại, tên', () => {
    expect(searchCountries('vn', countries).map(c => c.code)).toEqual(['VN']);
    expect(searchCountries('+84', countries).map(c => c.code)).toEqual(['VN']);
    expect(searchCountries('aus', countries).map(c => c.code)).toEqual(['AU', 'AT']);
    expect(searchCountries('', countries)).toEqual([]);
  });
});
