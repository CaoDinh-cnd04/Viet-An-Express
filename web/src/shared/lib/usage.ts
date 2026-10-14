import { apiUrl } from '@/shared/api/http';

export type OrderMode = 'wizard' | 'quick';
const paths = new Set(['/', '/login', '/orders/new', '/orders/new/quick', '/orders/import', '/orders', '/drafts', '/pricing', '/ecommerce', '/ecommerce/orders', '/troubles', '/notifications', '/help', '/account/api-tracking', '/account/mytracking', '/account/staff', '/account/password', '/account/feedback']);

export function usagePath(path: string): string | null {
  if (path === '/account/sender-info') return path;
  if (path === '/tracking' || path.startsWith('/tracking/')) return '/tracking';
  if (path.startsWith('/t/')) return '/t';
  return paths.has(path) ? path : null;
}

let visitor: string | undefined;
function visitorId(): string {
  if (visitor) return visitor;
  try {
    const saved = localStorage.getItem('va.usage.visitor');
    visitor = saved && /^[0-9a-f-]{36}$/i.test(saved) ? saved : crypto.randomUUID();
    localStorage.setItem('va.usage.visitor', visitor);
  } catch { visitor = crypto.randomUUID(); }
  return visitor;
}

// Analytics failure must never prevent navigation or saving an order. Retries reuse the event ID.
export function trackUsage(path: string, kind = 'page_view', mode?: OrderMode, flowId?: string): void {
  const normalized = usagePath(path);
  if (!normalized) return;
  const body = JSON.stringify({ id: crypto.randomUUID(), visitorId: visitorId(), path: normalized, kind, mode: mode ?? null, flowId: flowId ?? null });
  const send = async (attempt: number): Promise<void> => {
    try {
      const res = await fetch(apiUrl('/usage/events'), { method: 'POST', credentials: 'include', keepalive: true, headers: { 'Content-Type': 'application/json' }, body });
      if (res.ok || (res.status < 500 && res.status !== 429)) return;
    } catch { /* network unavailable */ }
    if (attempt < 2) window.setTimeout(() => { void send(attempt + 1); }, (attempt + 1) * 3000);
  };
  void send(0);
}

interface Flow { id: string; started: OrderMode[] }
const flows = new Map<string, Flow>();
export function orderUsage(scope: string, mode: OrderMode, kind: 'order_start' | 'order_complete' | 'mode_switch'): void {
  const key = `${scope}:usageFlow`;
  let flow = flows.get(key);
  if (!flow) {
    try {
      const raw = JSON.parse(sessionStorage.getItem(key) ?? 'null') as Flow | null;
      if (raw && /^[0-9a-f-]{36}$/i.test(raw.id) && Array.isArray(raw.started)) flow = raw;
    } catch { /* storage unavailable */ }
    flow ??= { id: crypto.randomUUID(), started: [] };
    flows.set(key, flow);
  }
  const path = mode === 'wizard' ? '/orders/new' : '/orders/new/quick';
  if (kind !== 'mode_switch' && !flow.started.includes(mode)) {
    trackUsage(path, 'order_start', mode, flow.id);
    flow.started.push(mode);
  }
  if (kind !== 'order_start') trackUsage(path, kind, mode, flow.id);
  try {
    if (kind === 'order_complete') sessionStorage.removeItem(key);
    else sessionStorage.setItem(key, JSON.stringify(flow));
  } catch { /* storage unavailable */ }
  if (kind === 'order_complete') flows.delete(key);
}
