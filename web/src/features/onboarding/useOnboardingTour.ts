import { useCallback, useEffect, useState } from 'react';
import { pageTourId, tourStorageKey, type TourId } from './lib/tour';

const read = (key: string) => {
  try {
    return window.localStorage.getItem(key) === '1';
  } catch {
    return false;
  }
};

const write = (key: string) => {
  try {
    window.localStorage.setItem(key, '1');
  } catch {
    /* storage bị chặn — lần sau hiện lại, không sao */
  }
};

/** Đang có hộp thoại khác mở (vd thông báo quan trọng) → chờ tắt rồi mới hiện hướng dẫn. */
const dialogOpen = () => !!document.querySelector('dialog[open]');

/**
 * Hướng dẫn tự mở 1 lần cho mỗi khách (nhớ trên trình duyệt): lần đầu đăng nhập là hướng dẫn tổng quát,
 * xem xong thì lần đầu vào trang có hướng dẫn riêng (Tạo đơn 1 trang, Đơn hàng…) sẽ hiện hướng dẫn trang đó.
 * Mở lại bằng start() (tổng quát) / startPage() (trang đang mở).
 * @param customerCode mã khách đang đăng nhập; chưa có (đang tải phiên) thì chưa tự mở.
 */
export function useOnboardingTour(customerCode: string | undefined, pathname: string) {
  const [active, setActive] = useState<TourId | null>(null);
  const pageTour = pageTourId(pathname);

  useEffect(() => {
    if (!customerCode || active) return;
    const id: TourId | undefined = read(tourStorageKey(customerCode)) ? pageTour : 'intro';
    if (!id || read(tourStorageKey(customerCode, id))) return;
    let timer = 0;
    const tryOpen = () => {
      if (dialogOpen()) timer = window.setTimeout(tryOpen, 1500);
      else setActive(id);
    };
    timer = window.setTimeout(tryOpen, 1200); // chờ trang vẽ xong menu và tải dữ liệu
    return () => window.clearTimeout(timer);
  }, [customerCode, pageTour, active]);

  const start = useCallback(() => setActive('intro'), []);
  const startPage = useCallback(() => {
    if (pageTour) setActive(pageTour);
  }, [pageTour]);

  /** Đóng (xong / bỏ qua / để sau) — đều tính là đã xem, không tự hiện lại. */
  const close = useCallback(() => {
    if (customerCode && active) write(tourStorageKey(customerCode, active));
    setActive(null);
  }, [customerCode, active]);

  return { active, pageTour, start, startPage, close };
}
