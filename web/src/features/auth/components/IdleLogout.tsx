import { useEffect, useRef } from 'react';
import { useLogout } from '../api';
import { isIdleExpired, LAST_ACTIVITY_KEY, runBeforeIdleLogout } from '../lib/idle';

const ACTIVITY_EVENTS = ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart', 'scroll'] as const;
/** Ghi lần thao tác cuối tối đa 1 lần / 5 giây (pointermove bắn liên tục). */
const WRITE_EVERY_MS = 5_000;
const CHECK_EVERY_MS = 15_000;

function readLast(fallback: number): number {
  try {
    const v = Number(window.localStorage.getItem(LAST_ACTIVITY_KEY));
    return Number.isFinite(v) && v > 0 ? v : fallback;
  } catch {
    return fallback;
  }
}

function writeLast(at: number) {
  try {
    window.localStorage.setItem(LAST_ACTIVITY_KEY, String(at));
  } catch {
    /* chế độ riêng tư / chặn lưu trữ: vẫn đếm trong tab này */
  }
}

/**
 * Tự đăng xuất khi khách không thao tác quá lâu (đặt trong khung trang đã đăng nhập), không hỏi trước.
 * Trước khi đăng xuất chạy việc trang đã đăng ký (Tạo đơn: lưu đơn đang làm vào nháp); khách quay lại thấy màn đăng nhập.
 * Lần thao tác cuối dùng chung mọi tab — đang dùng tab khác thì không bị đăng xuất.
 */
export function IdleLogout() {
  const logout = useLogout();
  const last = useRef(Date.now());
  const loggingOut = useRef(false);

  useEffect(() => {
    last.current = Date.now();
    writeLast(last.current);
    const onActivity = () => {
      const now = Date.now();
      if (now - last.current < WRITE_EVERY_MS) return;
      last.current = now;
      writeLast(now);
    };
    const check = () => {
      last.current = Math.max(last.current, readLast(last.current));
      if (loggingOut.current || !isIdleExpired(last.current, Date.now())) return;
      loggingOut.current = true;
      void runBeforeIdleLogout().then(next => logout.mutate({ idle: true, next }));
    };
    ACTIVITY_EVENTS.forEach(e => window.addEventListener(e, onActivity, { passive: true }));
    // Quay lại tab sau khi máy ngủ: kiểm tra ngay, không đợi lần hẹn giờ kế tiếp.
    document.addEventListener('visibilitychange', check);
    const timer = window.setInterval(check, CHECK_EVERY_MS);
    return () => {
      ACTIVITY_EVENTS.forEach(e => window.removeEventListener(e, onActivity));
      document.removeEventListener('visibilitychange', check);
      window.clearInterval(timer);
    };
    // logout.mutate ổn định trong suốt vòng đời component.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return null;
}
