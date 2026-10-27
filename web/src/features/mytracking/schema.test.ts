import { describe, expect, it } from 'vitest';
import { emptyConfig, isWebUrl, MAX_IMAGE_BYTES, myTrackingSchema, parseConfig } from './schema';
import { validateImageFile } from './lib/images';
describe('MyTracking settings', () => {
  it('mô tả và địa chỉ giữ giới hạn 250 ký tự của bảng cũ', () => {
    const config = emptyConfig();
    config.description = 'd'.repeat(250);
    config.brand.address = 'a'.repeat(250);
    expect(myTrackingSchema.safeParse(config).success).toBe(true);
    config.description += 'd';
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
    config.description = '';
    config.brand.address += 'a';
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
  });
  it('link quảng cáo dài hơn 250 bị từ chối trước khi lưu', () => {
    const config = emptyConfig();
    config.images = [{ id: '1', src: 'https://example.com/a.jpg', linkUrl: `https://example.com/${'a'.repeat(250)}` }];
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
  });
  it('giữ tiêu đề và chữ nút ảnh khi lưu, vẫn đọc được ảnh cũ', () => {
    const image = { id: '1', src: 'https://example.com/photo.jpg', linkUrl: 'https://example.com', title: 'Dịch vụ quốc tế', buttonText: 'Liên hệ' };
    expect(parseConfig({ ...emptyConfig(), images: [image] }).images[0]).toEqual(image);
    expect(myTrackingSchema.safeParse({ ...emptyConfig(), images: [{ ...image, title: 'a'.repeat(121) }] }).success).toBe(false);
    expect(myTrackingSchema.safeParse({ ...emptyConfig(), images: [{ ...image, buttonText: 'a'.repeat(41) }] }).success).toBe(false);
  });
  it('cổng liên lạc chặn giao thức không an toàn', () => {
    const config = emptyConfig();
    config.brand.contacts = [{ type: 'zalo', value: '0912345678' }, { type: 'whatsapp', value: '+12025550123' }];
    expect(myTrackingSchema.safeParse(config).success).toBe(true);
    config.brand.contacts = [{ type: 'zalo', value: 'javascript:alert(1)' }];
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
  });
  it('chặn link javascript cho quảng cáo và mạng xã hội', () => {
    expect(isWebUrl('https://example.com')).toBe(true);
    for (const url of ['javascript:alert(1)', 'file:///photo.jpg', 'data:text/html,test', 'invalid']) expect(isWebUrl(url)).toBe(false);
    expect(myTrackingSchema.safeParse({ ...emptyConfig(), images: [{ id: '1', src: 'https://example.com/photo.jpg', linkUrl: 'javascript:alert(1)' }] }).success).toBe(false);
    const config = emptyConfig(); config.brand.socials = [{ type: 'facebook', value: 'javascript:alert(1)' }];
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
  });
  it('cổng liên lạc / mạng xã hội: tối đa 5 mục, không trùng loại', () => {
    const config = emptyConfig();
    config.brand.contacts = [
      { type: 'hotline', value: '0909123456' }, { type: 'zalo', value: '0909123456' }, { type: 'email', value: 'a@b.vn' },
      { type: 'telegram', value: '@va' }, { type: 'viber', value: '0909123456' }
    ];
    expect(myTrackingSchema.safeParse(config).success).toBe(true);
    config.brand.contacts = [...config.brand.contacts, { type: 'line', value: 'va' }];
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
    config.brand.contacts = [{ type: 'zalo', value: '0909123456' }, { type: 'zalo', value: '0909123457' }];
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
  });
  it('màu thương hiệu dạng #rrggbb hoặc trống', () => {
    const config = emptyConfig();
    config.brand.primaryColor = '#1e6b2c';
    expect(myTrackingSchema.safeParse(config).success).toBe(true);
    config.brand.secondaryColor = 'red';
    expect(myTrackingSchema.safeParse(config).success).toBe(false);
  });
  it('cấu hình cũ chưa có danh sách chọn: dựng từ các ô cũ', () => {
    const old = parseConfig({ title: 'A', brand: { phone: '0909 111 222', zalo: '0909123456', facebook: 'https://facebook.com/va' } });
    expect(old.title).toBe('A');
    expect(old.brand.contacts).toEqual([{ type: 'hotline', value: '0909 111 222' }, { type: 'zalo', value: '0909123456' }]);
    expect(old.brand.socials).toEqual([{ type: 'facebook', value: 'https://facebook.com/va' }]);
    expect(old.brand.website).toBe('');
    expect(parseConfig({ brand: { zalo: '0909123456', contacts: [] } }).brand.contacts).toEqual([]);
    // Giá trị cũ không hợp lệ: bỏ riêng mục đó, giữ phần còn lại của cấu hình.
    const bad = parseConfig({ title: 'B', brand: { phone: '12', contacts: [{ type: 'zalo', value: 'x' }, { type: 'email', value: 'a@b.vn' }] } });
    expect(bad.title).toBe('B');
    expect(bad.brand.contacts).toEqual([{ type: 'email', value: 'a@b.vn' }]);
  });
  it('ảnh hệ thống cũ lưu đường dẫn tương đối vẫn hiển thị', () => {
    const parsed = parseConfig({ title: 'A', background: '/Upload/Banner/bg.jpg', brand: { logo: '/upload/Logo_ShippingMark/l.png' },
      images: [{ id: '1', src: '/Upload/Banner/ad.jpg', linkUrl: '' }] });
    expect(parsed.title).toBe('A');
    expect(parsed.background).toBe('https://mytracking.top/Upload/Banner/bg.jpg');
    expect(parsed.brand.logo).toBe('https://mytracking.top/upload/Logo_ShippingMark/l.png');
    expect(parsed.images[0]?.src).toBe('https://mytracking.top/Upload/Banner/ad.jpg');
  });
  it('tối đa 5 ảnh, link đích có thể trống', () => {
    const image = { id: '1', src: 'https://example.com/photo.jpg', linkUrl: '' };
    expect(myTrackingSchema.safeParse({ ...emptyConfig(), images: Array.from({ length: 5 }, () => image) }).success).toBe(true);
    expect(myTrackingSchema.safeParse({ ...emptyConfig(), images: Array.from({ length: 6 }, () => image) }).success).toBe(false);
  });
  it('giữ cấu hình cũ và link quảng cáo, bổ sung thương hiệu trống', () => {
    const parsed = parseConfig({ title: 'Shop ABC', description: '', background: '', images: [{ id: '1', src: 'https://example.com/a.png', link: 'https://example.com' }] });
    expect(parsed.title).toBe('Shop ABC'); expect(parsed.images[0]?.linkUrl).toBe('https://example.com'); expect(parsed.brand.companyName).toBe('');
    expect(parseConfig({ title: 123 })).toEqual(emptyConfig());
  });
  it('chỉ nhận JPG/PNG/WebP tối đa 200 KB trước khi nén', () => {
    expect(() => validateImageFile({ type: 'image/jpeg', size: MAX_IMAGE_BYTES })).not.toThrow();
    expect(() => validateImageFile({ type: 'image/png', size: MAX_IMAGE_BYTES + 1 })).toThrow('200 KB');
    expect(() => validateImageFile({ type: 'image/gif', size: 10 })).toThrow('Chỉ nhận ảnh');
    expect(() => validateImageFile({ type: 'image/svg+xml', size: 10 })).toThrow('Chỉ nhận ảnh');
  });
});
