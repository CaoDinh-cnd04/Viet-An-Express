import { describe, expect, it } from 'vitest';
import { needsReauthorize, readOAuthResult, resolveEcomTab, formatSyncTime } from './store-connection';

describe('needsReauthorize', () => {
  it('chỉ expired / revoked cần ủy quyền lại', () => {
    expect(needsReauthorize({ status: 'expired' })).toBe(true);
    expect(needsReauthorize({ status: 'revoked' })).toBe(true);
    expect(needsReauthorize({ status: 'active' })).toBe(false);
    expect(needsReauthorize({ status: 'error' })).toBe(false);
  });
});

describe('readOAuthResult', () => {
  it('đọc kết quả redirect', () => {
    expect(readOAuthResult(new URLSearchParams('tab=connect&connected=tiktok'))).toEqual({ ok: true, platform: 'tiktok' });
    expect(readOAuthResult(new URLSearchParams('tab=connect&error=Shop từ chối'))).toEqual({ ok: false, error: 'Shop từ chối' });
    expect(readOAuthResult(new URLSearchParams('tab=connect'))).toBeNull();
  });
});

describe('resolveEcomTab', () => {
  it('giữ tab hợp lệ, đổi tab cũ sang tab mới, mặc định là đơn hàng', () => {
    expect(resolveEcomTab('add')).toBe('add');
    expect(resolveEcomTab('mine')).toBe('orders'); // trang chuyển hướng ?tab=mine sang /ecommerce/orders
    expect(resolveEcomTab('conn')).toBe('connect');
    expect(resolveEcomTab('stores')).toBe('connect');
    expect(resolveEcomTab('push')).toBe('add');
    expect(resolveEcomTab('overview')).toBe('orders');
    expect(resolveEcomTab('khac')).toBe('orders');
    expect(resolveEcomTab(null)).toBe('orders');
  });
});

describe('formatSyncTime', () => {
  it('lấy ngày giờ theo múi giờ backend gửi', () => {
    expect(formatSyncTime('2026-10-05T14:54:25.6+07:00')).toBe('05/10/2026 14:54');
    expect(formatSyncTime('khác')).toBe('khác');
  });
});

