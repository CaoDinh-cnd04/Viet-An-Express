import { describe, expect, it } from 'vitest';
import { resolveApiUrl } from './http';

describe('resolveApiUrl', () => {
  const page = 'http://beta.vietanexpress.com.vn';

  it('backend cùng tên miền: đường dẫn tương đối', () => {
    expect(resolveApiUrl('/api/v1', '/orders', { page: 2, q: '' }, page)).toBe('/api/v1/orders?page=2');
  });

  it('backend ở tên miền riêng: giữ tên miền backend', () => {
    expect(resolveApiUrl('http://api.vietanexpress.com.vn/api/v1', '/auth/session', undefined, page))
      .toBe('http://api.vietanexpress.com.vn/api/v1/auth/session');
    expect(resolveApiUrl('http://api.vietanexpress.com.vn/api/v1', '/orders/print', { bills: 'A,B', doc: 'invoice' }, page))
      .toBe('http://api.vietanexpress.com.vn/api/v1/orders/print?bills=A%2CB&doc=invoice');
  });
});
