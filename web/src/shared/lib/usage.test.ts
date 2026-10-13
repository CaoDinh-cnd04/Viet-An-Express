import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { orderUsage, trackUsage, usagePath } from './usage';

describe('usage tracking', () => {
  const storage = new Map<string, string>();
  beforeEach(() => {
    storage.clear();
    const store = { getItem: (k: string) => storage.get(k) ?? null, setItem: (k: string, v: string) => storage.set(k, v), removeItem: (k: string) => storage.delete(k) };
    vi.stubGlobal('localStorage', store);
    vi.stubGlobal('sessionStorage', store);
    vi.stubGlobal('window', { location: { origin: 'http://localhost' }, setTimeout });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true }));
  });
  afterEach(() => vi.unstubAllGlobals());
  it('normalizes public routes and excludes admin, unknown routes and raw queries', () => {
    expect(usagePath('/tracking/PRIVATE-BILL')).toBe('/tracking');
    expect(usagePath('/t/customer-secret')).toBe('/t');
    expect(usagePath('/admin')).toBeNull();
    expect(usagePath('/orders/new?draft=1')).toBeNull();
    trackUsage('/tracking/PRIVATE-BILL');
    expect(JSON.parse(vi.mocked(fetch).mock.calls[0]![1]!.body as string).path).toBe('/tracking');
  });
  it('deduplicates starts, preserves flow across modes, resets after completion', () => {
    const scope = crypto.randomUUID();
    orderUsage(scope, 'wizard', 'order_start');
    orderUsage(scope, 'wizard', 'order_start');
    orderUsage(scope, 'quick', 'mode_switch');
    orderUsage(scope, 'quick', 'order_start');
    orderUsage(scope, 'quick', 'order_complete');
    orderUsage(scope, 'wizard', 'order_start');
    const events = vi.mocked(fetch).mock.calls.map(c => JSON.parse(c[1]!.body as string) as { kind: string; flowId: string; mode: string });
    expect(events.map(e => e.kind)).toEqual(['order_start', 'mode_switch', 'order_start', 'order_complete', 'order_start']);
    expect(new Set(events.slice(0, 4).map(e => e.flowId)).size).toBe(1);
    expect(events[4]!.flowId).not.toBe(events[0]!.flowId);
  });
});
