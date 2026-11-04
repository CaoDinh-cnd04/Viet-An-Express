import { describe, expect, it } from 'vitest';
import { CARRIER_HUBS, CARRIERS, carriersFor, defaultHub, hubLabel, hubOptions, hubsFor, stateRequired } from './domain';

describe('hãng / hub', () => {
  it('đủ 8 hãng theo hệ thống cũ, giá trị hub dạng "Hãng - Hub"', () => {
    expect(CARRIERS).toEqual(['Aramex', 'DHL', 'Fedex', 'UPS', 'Chuyên tuyến', 'Ủy quyền Việt An', 'Ecommerce', 'SEA']);
    expect(CARRIER_HUBS.Fedex).toContain('Fedex - SIN 3 (FICP)');
    expect(CARRIER_HUBS['Chuyên tuyến']).toContain('Chuyên tuyến - Singapore');
  });

  it('nhãn chỉ hiện tên hub, kể cả hub có dấu gạch', () => {
    expect(hubLabel('Chuyên tuyến - USA - Sea')).toBe('USA - Sea');
    expect(hubLabel('DHL - SIN VIP')).toBe('SIN VIP');
    expect(hubOptions('Ecommerce')).toEqual([
      { value: 'Ecommerce - SING POST', label: 'SING POST' },
      { value: 'Ecommerce - Việt An', label: 'Việt An' }
    ]);
    expect(hubOptions('Không có')).toEqual([]);
  });

  it('đổi hãng: 1 hub thì chọn luôn, nhiều hub thì để khách chọn', () => {
    expect(defaultHub('Aramex')).toBe('Aramex - Dubai');
    expect(defaultHub('Ủy quyền Việt An')).toBe('Ủy quyền Việt An - Tự chọn');
    expect(defaultHub('DHL')).toBe('');
  });
});

describe('hub theo nước đến (bảng CS điền + nước hãng không phục vụ)', () => {
  it('chuyên tuyến chỉ hiện hub đúng nước, hub VA đi mọi nước', () => {
    expect(hubsFor('Chuyên tuyến', 'AU')).toEqual([
      'Chuyên tuyến - AU_AuPost', 'Chuyên tuyến - AU_Toll (T.Phẩm/M.Phẩm)', 'Chuyên tuyến - AU_Toll Vip (Hàng thường)', 'Chuyên tuyến - VA'
    ]);
    expect(hubsFor('Chuyên tuyến', 'de')).toEqual(['Chuyên tuyến - EU', 'Chuyên tuyến - VA']);
    expect(hubsFor('Chuyên tuyến', 'BR')).toEqual(['Chuyên tuyến - VA']);
  });

  it('UPS Dubai / Aramex Dubai theo danh sách nước; hãng đi mọi nước vẫn ẩn nước không phục vụ', () => {
    expect(hubsFor('UPS', 'DE')).toContain('UPS - Dubai');
    expect(hubsFor('UPS', 'US')).not.toContain('UPS - Dubai');
    expect(hubsFor('Aramex', 'NG')).toEqual(['Aramex - Dubai']);
    expect(hubsFor('Aramex', 'US')).toEqual([]);
    expect(hubsFor('DHL', 'US')).toEqual(CARRIER_HUBS.DHL);
    expect(hubsFor('DHL', 'TJ')).toEqual([]);   // DHL không phục vụ Tajikistan
    expect(carriersFor('RU')).not.toContain('Fedex'); // Fedex tạm ngưng Nga
    expect(carriersFor('KH')).not.toContain('UPS');   // UPS đình chỉ Campuchia
    expect(carriersFor('KH')).toContain('DHL');
  });

  it('chưa biết mã nước thì không lọc; chỉ còn 1 hub thì chọn sẵn', () => {
    expect(hubsFor('Chuyên tuyến', undefined)).toEqual(CARRIER_HUBS['Chuyên tuyến']);
    expect(defaultHub('SEA', 'AU')).toBe('SEA - Australia');
    expect(hubOptions('Chuyên tuyến', 'JP').map(o => o.label)).toEqual(['Japan', 'VA']);
  });
});

describe('Tỉnh / bang bắt buộc', () => {
  it('Mỹ, Canada, Úc — theo mã nước, thiếu mã thì theo tên', () => {
    expect(stateRequired('United States', 'US')).toBe(true);
    expect(stateRequired('', 'ca')).toBe(true);
    expect(stateRequired('Australia')).toBe(true);
    expect(stateRequired('Singapore', 'SG')).toBe(false);
    expect(stateRequired('Germany')).toBe(false);
  });
});
