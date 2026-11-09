import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { apiUrl } from './http';

/**
 * Kết nối thời gian thực (SignalR) tới backend — hiện dùng cho Hỗ trợ & góp ý (hub /live/support, backend Support/SupportLive.cs).
 * Máy chủ chỉ báo "có thay đổi" (vd mã yêu cầu); trang nhận tín hiệu thì tải lại dữ liệu qua API như thường.
 * 1 kết nối dùng chung cho cả tab: mở khi có trang đăng ký đầu tiên, đóng khi không còn ai. Dùng cookie đăng nhập như API.
 * Mất kết nối / chưa đăng nhập được thì tự thử lại (2–15 giây); IIS chưa bật WebSocket thì SignalR tự chuyển sang SSE / long polling.
 */

type Handler = (...args: unknown[]) => void;

const HUB_PATH = '/live/support';
const RETRY_MS = [0, 2_000, 5_000, 10_000];
const RETRY_FOREVER_MS = 15_000;

let connection: HubConnection | null = null;
let starting: Promise<void> | null = null;
let retryTimer: ReturnType<typeof setTimeout> | undefined;
const handlers = new Map<string, Set<Handler>>();

const dispatch = (event: string, args: unknown[]) => handlers.get(event)?.forEach(h => h(...args));

function build(): HubConnection {
  const c = new HubConnectionBuilder()
    .withUrl(apiUrl(HUB_PATH), { withCredentials: true })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: ctx => RETRY_MS[ctx.previousRetryCount] ?? RETRY_FOREVER_MS })
    .configureLogging(LogLevel.None)
    .build();
  for (const event of handlers.keys()) c.on(event, (...args: unknown[]) => dispatch(event, args));
  // Hết lượt tự nối lại (hoặc máy chủ đóng) → thử lại chậm, miễn còn trang đăng ký.
  c.onclose(() => scheduleStart(RETRY_FOREVER_MS));
  return c;
}

function scheduleStart(delay: number) {
  clearTimeout(retryTimer);
  if (!handlers.size) return;
  retryTimer = setTimeout(() => void start(), delay);
}

async function start() {
  if (!handlers.size || !connection || connection.state !== HubConnectionState.Disconnected || starting) return;
  starting = connection.start()
    .catch(() => scheduleStart(RETRY_FOREVER_MS)) // chưa đăng nhập / mất mạng / máy chủ khởi động lại
    .finally(() => { starting = null; });
  await starting;
}

/** Đăng ký nhận 1 loại tín hiệu; trả hàm hủy đăng ký. */
export function subscribeLive(event: string, handler: Handler): () => void {
  let set = handlers.get(event);
  if (!set) {
    set = new Set();
    handlers.set(event, set);
    // Kết nối đã có: gắn thêm loại tín hiệu mới.
    connection?.on(event, (...args: unknown[]) => dispatch(event, args));
  }
  set.add(handler);
  connection ??= build();
  void start();

  return () => {
    set.delete(handler);
    if (set.size) return;
    handlers.delete(event);
    connection?.off(event);
    if (handlers.size) return;
    clearTimeout(retryTimer);
    const c = connection;
    connection = null;
    void c?.stop();
  };
}

/** Hook: gọi `handler` mỗi khi máy chủ phát tín hiệu `event` (handler mới nhất, không cần useCallback). */
export function useLiveEvent(event: string, handler: Handler, enabled = true) {
  const ref = useRef(handler);
  useEffect(() => {
    ref.current = handler;
  });
  useEffect(() => {
    if (!enabled) return;
    return subscribeLive(event, (...args) => ref.current(...args));
  }, [event, enabled]);
}
