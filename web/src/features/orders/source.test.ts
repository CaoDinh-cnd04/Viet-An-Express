import { describe, expect, it } from 'vitest';
import { ordersApiBase } from './source';

describe('ordersApiBase', () => {
  it('đơn của tôi gọi /orders, đơn khách phụ trách gọi /vietan/orders', () => {
    expect(ordersApiBase('mine')).toBe('/orders');
    expect(ordersApiBase('managed')).toBe('/vietan/orders');
  });
});
