import { describe, expect, it } from 'vitest';
import { isPendingEndpoint } from './pending';

describe('isPendingEndpoint', () => {
  it('nhận cả endpoint con và query', () => {
    expect(isPendingEndpoint('/ecom/settings')).toBe(true);
    expect(isPendingEndpoint('/ecom/settings?tab=api')).toBe(true);
    expect(isPendingEndpoint('/ecom/settings/api-keys/regenerate')).toBe(true);
  });

  it('không chặn endpoint đã có', () => {
    expect(isPendingEndpoint('/ecom/stores')).toBe(false);
    expect(isPendingEndpoint('/notifications')).toBe(false);
    expect(isPendingEndpoint('/troubles')).toBe(false);
    expect(isPendingEndpoint('/ecom/settingsx')).toBe(false);
    expect(isPendingEndpoint('/orders')).toBe(false);
  });
});
