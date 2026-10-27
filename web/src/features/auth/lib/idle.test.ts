import { describe, expect, it } from 'vitest';
import { IDLE_TIMEOUT_MS, isIdleExpired, onBeforeIdleLogout, runBeforeIdleLogout } from './idle';

describe('isIdleExpired', () => {
  const start = 1_000_000;

  it('chưa đủ 30 phút không thao tác thì chưa đăng xuất', () => {
    expect(isIdleExpired(start, start + IDLE_TIMEOUT_MS - 1)).toBe(false);
  });

  it('đủ 30 phút thì đăng xuất', () => {
    expect(isIdleExpired(start, start + IDLE_TIMEOUT_MS)).toBe(true);
  });
});

describe('runBeforeIdleLogout', () => {
  it('chạy việc đã đăng ký, lấy trang muốn mở; việc lỗi / treo không chặn đăng xuất', async () => {
    const off = [
      onBeforeIdleLogout(() => Promise.resolve('/drafts')),
      onBeforeIdleLogout(() => Promise.reject(new Error('mất mạng'))),
      onBeforeIdleLogout(() => new Promise(() => undefined))
    ];
    expect(await runBeforeIdleLogout(20)).toBe('/drafts');
    off.forEach(f => f());
    expect(await runBeforeIdleLogout(20)).toBeUndefined();
  });
});
