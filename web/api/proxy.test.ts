import { afterEach, expect, it, vi } from 'vitest';
import handler from './proxy';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

it.each([200, 401])('forwards signed webhook bytes and headers without Expect, preserving HTTP %s', async status => {
  vi.stubEnv('BACKEND_URL', 'https://backend.example/');
  const body = '{\n  "customer": {"name": "Việt An"}\n}';
  const hmac = 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=';
  const fetchMock = vi.fn(async (url: string, init: RequestInit) => {
    const headers = new Headers(init.headers);
    // Fetch rejects this handshake header; it must not reach the upstream request.
    if (headers.has('expect')) throw new Error('Expect is unsupported');
    expect(url).toBe('https://backend.example/api/v1/ecom/webhooks/test');
    expect(headers.get('X-Signature')).toBe(hmac);
    expect(headers.get('X-Event')).toBe('order.created');
    expect(init.redirect).toBe('manual');
    const forwarded = new Request(url, init);
    expect(new Uint8Array(await forwarded.arrayBuffer())).toEqual(new TextEncoder().encode(body));
    return new Response(null, { status });
  });
  vi.stubGlobal('fetch', fetchMock);

  const request = new Request('https://portal.example/api/proxy?__path=v1/ecom/webhooks/test', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Expect': '100-continue',
      'X-Signature': hmac,
      'X-Event': 'order.created'
    },
    body
  });

  expect((await handler(request)).status).toBe(status);
  expect(fetchMock).toHaveBeenCalledOnce();
});
