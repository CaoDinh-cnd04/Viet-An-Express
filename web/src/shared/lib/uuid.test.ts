import { describe, expect, it } from 'vitest';
import { uuid } from './uuid';

const V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe('uuid', () => {
  it('trả UUID v4', () => {
    expect(uuid()).toMatch(V4);
  });

  it('vẫn chạy khi trình duyệt không có crypto.randomUUID (trang HTTP)', () => {
    Object.defineProperty(crypto, 'randomUUID', { value: undefined, configurable: true });
    try {
      const a = uuid();
      expect(a).toMatch(V4);
      expect(uuid()).not.toBe(a);
    } finally {
      delete (crypto as { randomUUID?: unknown }).randomUUID;
    }
  });
});
