import { useEffect, useRef } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useSession } from '@/features/auth';
import { orderUsage, trackUsage, usagePath } from '@/shared/lib/usage';

export function UsageTrackingLayout() {
  const { pathname, search, key } = useLocation();
  const session = useSession();
  const last = useRef<string | undefined>(undefined);
  const previous = useRef<{ path: string; account: string; draft: boolean } | null>(null);
  const account = session.data?.status === 'authenticated' ? `${session.data.user.customerCode}:${session.data.user.userName}` : 'anonymous';
  useEffect(() => {
    if (session.isPending) return;
    if (!usagePath(pathname)) { previous.current = null; return; }
    const view = `${key}:${account}`;
    if (last.current === view) return;
    last.current = view;
    const draft = new URLSearchParams(search).has('draft');
    const isOrderMode = (path: string) => path === '/orders/new' || path === '/orders/new/quick';
    const prev = previous.current;
    if (session.data?.status === 'authenticated' && prev?.account === account && prev.path !== pathname
      && !prev.draft && !draft && isOrderMode(prev.path) && isOrderMode(pathname)) {
      const user = session.data.user;
      orderUsage(`va.createOrder.autosave:${user.customerCode}:${user.userName ?? ''}`, pathname === '/orders/new' ? 'wizard' : 'quick', 'mode_switch');
    }
    previous.current = { path: pathname, account, draft };
    trackUsage(pathname);
  }, [pathname, search, key, account, session.isPending, session.data]);
  return <Outlet />;
}
