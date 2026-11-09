import { describe, expect, it } from 'vitest';
import { addressQuery, canAutofill, findCountry, normalizePostal, postalOptions, shouldResetAddress, suggestionFields, type Country, type PostalSuggestion } from './geo';

const countries: Country[] = [
  { code: 'US', name: 'United States', dialCode: '+1' },
  { code: 'VN', name: 'Vietnam', dialCode: '+84' },
  { code: 'GB', name: 'United Kingdom', dialCode: '+44' }
];

describe('findCountry', () => {
  it('khớp tên không phân biệt hoa thường, bỏ khoảng trắng thừa', () => {
    expect(findCountry(countries, 'united  states ')?.dialCode).toBe('+1');
    expect(findCountry(countries, 'VIETNAM')?.code).toBe('VN');
  });
  it('gõ mã ISO 2 ký tự', () => {
    expect(findCountry(countries, 'gb')?.name).toBe('United Kingdom');
  });
  it('không khớp hoặc trống', () => {
    expect(findCountry(countries, 'United')).toBeUndefined();
    expect(findCountry(countries, '')).toBeUndefined();
  });
});

describe('normalizePostal', () => {
  it('chuẩn hoá và chấp nhận mã hợp lệ', () => {
    expect(normalizePostal(' 94526 ')).toBe('94526');
    expect(normalizePostal('ec1a 1bb')).toBe('EC1A 1BB');
  });
  it('quá ngắn hoặc ký tự lạ thì không tra', () => {
    expect(normalizePostal('12')).toBeNull();
    expect(normalizePostal('94526/..')).toBeNull();
    expect(normalizePostal(undefined)).toBeNull();
  });
});

describe('canAutofill', () => {
  it('ô trống thì điền', () => expect(canAutofill('', undefined)).toBe(true));
  it('giá trị do hệ thống điền trước đó thì được thay', () => expect(canAutofill('Danville', 'Danville')).toBe(true));
  it('khách đã tự gõ thì không ghi đè', () => expect(canAutofill('San Ramon', 'Danville')).toBe(false));
});

describe('shouldResetAddress', () => {
  it('đổi sang nước khác thì xoá', () => expect(shouldResetAddress('US', 'GB')).toBe(true));
  it('vẫn nước cũ, chọn nước lần đầu, hoặc đang gõ dở thì không xoá', () => {
    expect(shouldResetAddress('US', 'US')).toBe(false);
    expect(shouldResetAddress(undefined, 'US')).toBe(false);
    expect(shouldResetAddress('US', undefined)).toBe(false);
  });
});

describe('addressQuery', () => {
  it('gộp khoảng trắng, đủ 3 ký tự mới gợi ý', () => {
    expect(addressQuery('  123   Main ')).toBe('123 Main');
    expect(addressQuery('12')).toBeNull();
    expect(addressQuery(undefined)).toBeNull();
    expect(addressQuery('x'.repeat(121))).toBeNull();
  });
});

describe('suggestionFields', () => {
  it('điền địa chỉ, thành phố, tỉnh, mã bưu chính có trong gợi ý', () => {
    expect(suggestionFields({ label: '123 Main St, Austin', address1: '123 Main St', city: 'Austin', state: 'Texas', postalCode: '78701', countryCode: 'US' }))
      .toEqual([['addr1', '123 Main St'], ['city', 'Austin'], ['state', 'Texas'], ['postal', '78701']]);
  });
  it('gợi ý cấp thành phố không ghi đè ô địa chỉ khách đã gõ', () => {
    expect(suggestionFields({ label: 'Austin, TX', address1: '', city: 'Austin', state: 'Texas', postalCode: null, countryCode: 'US' }))
      .toEqual([['city', 'Austin'], ['state', 'Texas']]);
  });
  it('không ghi đè thành phố / tỉnh / mã bưu chính khách đã có, chỉ điền ô trống', () => {
    expect(suggestionFields(
      { label: 'Ras Al Khor Industrial 3, Dubai', address1: 'Ras Al Khor Industrial 3', city: 'Dubai', state: 'Dubai Emirate', postalCode: '00000', countryCode: 'AE' },
      { city: 'Dubai', state: '', postal: '34530 85864' }
    )).toEqual([['addr1', 'Ras Al Khor Industrial 3'], ['state', 'Dubai Emirate']]);
  });
});

describe('postalOptions', () => {
  it('MX: các khu "khu-thành phố" rồi thành phố, theo từng mã', () => {
    const items: PostalSuggestion[] = [
      { postalCode: '16090', city: 'Xochimilco', state: 'Distrito Federal', area: 'Barrio San Pedro' },
      { postalCode: '16090', city: 'Xochimilco', state: 'Distrito Federal', area: 'Caltongo' },
      { postalCode: '16095', city: 'Xochimilco', state: 'Distrito Federal', area: 'Delegación Política Xochimilco' }
    ];
    expect(postalOptions(items)).toEqual([
      { postalCode: '16090', city: 'Barrio San Pedro-Xochimilco' },
      { postalCode: '16090', city: 'Caltongo-Xochimilco' },
      { postalCode: '16090', city: 'Xochimilco' },
      { postalCode: '16095', city: 'Delegación Política Xochimilco-Xochimilco' },
      { postalCode: '16095', city: 'Xochimilco' }
    ]);
  });

  it('nước không có khu vực → mỗi mã 1 dòng thành phố, xếp tăng dần, bỏ trùng', () => {
    const items: PostalSuggestion[] = [
      { postalCode: '10002', city: 'New York', state: 'New York' },
      { postalCode: '10001', city: 'New York', state: 'New York' },
      { postalCode: '10001', city: 'New York', state: 'New York' }
    ];
    expect(postalOptions(items)).toEqual([
      { postalCode: '10001', city: 'New York' },
      { postalCode: '10002', city: 'New York' }
    ]);
  });
});
