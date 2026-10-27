/** Không thao tác (chuột, phím, chạm, cuộn) quá lâu thì tự đăng xuất — tính chung mọi tab đang mở portal. */
export const IDLE_TIMEOUT_MS = 30 * 60_000;
/** Khóa localStorage lưu lần thao tác cuối — tab khác có thao tác thì tab này cũng không bị đăng xuất. */
export const LAST_ACTIVITY_KEY = 'va.lastActivity';
/** Chờ tối đa việc cần làm trước khi tự đăng xuất (vd lưu đơn đang tạo vào nháp) — quá thì vẫn đăng xuất. */
export const BEFORE_IDLE_LOGOUT_TIMEOUT_MS = 15_000;

/** Hết thời gian chưa. Hàm thuần — có test. */
export const isIdleExpired = (lastActivity: number, now: number, timeoutMs = IDLE_TIMEOUT_MS) => now - lastActivity >= timeoutMs;

/** Việc cần làm trước khi tự đăng xuất; trả về đường dẫn muốn mở sau khi đăng nhập lại (vd '/drafts'). */
export type BeforeIdleLogout = () => Promise<string | void>;

const handlers = new Set<BeforeIdleLogout>();

/** Trang đăng ký việc cần làm trước khi tự đăng xuất (vd Tạo đơn lưu nháp). Trả về hàm hủy đăng ký. */
export function onBeforeIdleLogout(handler: BeforeIdleLogout): () => void {
  handlers.add(handler);
  return () => handlers.delete(handler);
}

/** Chạy mọi việc đã đăng ký (lỗi / quá thời gian thì bỏ qua) — trả về trang muốn mở sau khi đăng nhập lại, nếu có. */
export async function runBeforeIdleLogout(timeoutMs = BEFORE_IDLE_LOGOUT_TIMEOUT_MS): Promise<string | undefined> {
  const timeout = new Promise<undefined>(resolve => setTimeout(() => resolve(undefined), timeoutMs));
  const results = await Promise.all([...handlers].map(h => Promise.race([h().catch(() => undefined), timeout])));
  return results.find((r): r is string => typeof r === 'string');
}
