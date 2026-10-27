import { describe, expect, it } from 'vitest';
import { channelsFromLegacy, channelUrl, isHexColor } from './channels';

describe('channelUrl', () => {
  it('link http(s) giữ nguyên', () => {
    expect(channelUrl('kakaotalk', 'https://open.kakao.com/o/abc')).toBe('https://open.kakao.com/o/abc');
    expect(channelUrl('googlemaps', 'https://maps.app.goo.gl/xyz')).toBe('https://maps.app.goo.gl/xyz');
  });

  it('số điện thoại → link ứng dụng, 0 đầu đổi thành 84', () => {
    expect(channelUrl('hotline', '0909 123 456')).toBe('tel:0909123456');
    expect(channelUrl('zalo', '0909 123 456')).toBe('https://zalo.me/84909123456');
    expect(channelUrl('whatsapp', '+65 9123 4567')).toBe('https://wa.me/6591234567');
    expect(channelUrl('viber', '0909123456')).toBe('viber://chat?number=%2B84909123456');
  });

  it('tên tài khoản → link mạng xã hội', () => {
    expect(channelUrl('telegram', '@vietan')).toBe('https://t.me/vietan');
    expect(channelUrl('tiktok', '@vietan')).toBe('https://www.tiktok.com/@vietan');
    expect(channelUrl('youtube', 'vietan')).toBe('https://www.youtube.com/@vietan');
    expect(channelUrl('twitter', 'vietan')).toBe('https://x.com/vietan');
    expect(channelUrl('messenger', 'vietan.express')).toBe('https://m.me/vietan.express');
  });

  it('email và giá trị không hợp lệ', () => {
    expect(channelUrl('email', 'cs@vietan.vn')).toBe('mailto:cs@vietan.vn');
    expect(channelUrl('email', 'không phải email')).toBe('');
    expect(channelUrl('hotline', 'abc')).toBe('');
    expect(channelUrl('googlemaps', 'cửa hàng')).toBe('');
    expect(channelUrl('zalo', '')).toBe('');
  });
});

describe('channelsFromLegacy', () => {
  const legacy = { phone: '0909', zalo: '0909123456', whatsapp: '', facebook: 'https://facebook.com/va', instagram: '', x: 'https://x.com/va' };

  it('dựng danh sách từ các ô cũ', () => {
    expect(channelsFromLegacy(legacy)).toEqual({
      contacts: [{ type: 'hotline', value: '0909' }, { type: 'zalo', value: '0909123456' }],
      socials: [{ type: 'facebook', value: 'https://facebook.com/va' }, { type: 'twitter', value: 'https://x.com/va' }]
    });
  });
});

describe('isHexColor', () => {
  it('chỉ nhận #rrggbb', () => {
    expect(isHexColor('#1E6B2C')).toBe(true);
    expect(isHexColor('1e6b2c')).toBe(false);
    expect(isHexColor('#fff')).toBe(false);
  });
});
