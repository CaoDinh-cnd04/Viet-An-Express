import { Suspense, useEffect, useRef, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useLocalStorage } from '@/shared/lib/useLocalStorage';
import { cx } from '@/shared/lib/cx';
import { useSession } from '@/features/auth';
import { ImportantNoticeModal } from '@/features/notifications';
import { OnboardingTour, useOnboardingTour } from '@/features/onboarding';
import { useI18n } from '@/shared/i18n';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';
import styles from './AppShell.module.css';

const MOBILE_QUERY = '(max-width: 1040px)';
/** Trang bảng nhiều cột dùng hết chiều ngang màn hình (bỏ giới hạn 1560px); cả trang cuộn bình thường. */
const FULL_SCREEN_PATHS = ['/orders'];
/** Cuộn quá mốc này (px) thì thu gọn menu; cuộn lên lại không tự mở — rê chuột vào menu mới hiện. */
const SCROLL_COLLAPSE_AT = 120;

/** Khung trang: sidebar + topbar + nội dung route. */
export function AppShell() {
  const [collapsed, setCollapsed] = useLocalStorage('va.nav.collapsed', false);
  const [mobileOpen, setMobileOpen] = useState(false);
  // Khách bắt đầu gõ ở bất kỳ trang nào → thu gọn tạm menu (không ghi đè lựa chọn đã lưu); rê chuột vào menu thì mở đè lên nội dung.
  const [autoCollapsed, setAutoCollapsed] = useState(false);
  const [peek, setPeek] = useState(false);
  const { pathname } = useLocation();
  const { t } = useI18n();
  const session = useSession();
  const customerCode = session.data?.status === 'authenticated' ? session.data.user.customerCode : undefined;
  const tour = useOnboardingTour(customerCode);

  const menuCollapsed = collapsed || autoCollapsed;

  // Sang trang khác → menu trở lại như khách đã chọn.
  useEffect(() => {
    setAutoCollapsed(false);
    setPeek(false);
  }, [pathname]);

  // Trang bảng (Đơn hàng của tôi): cuộn xuống → thu gọn tạm menu cho bảng rộng hơn. Cuộn lên không mở lại;
  // rê chuột vào menu thì hiện đè lên nội dung, bấm nút menu / Ctrl+B thì mở hẳn.
  useEffect(() => {
    if (!FULL_SCREEN_PATHS.includes(pathname)) return;
    let frame = 0;
    const onScroll = () => {
      if (frame) return;
      frame = requestAnimationFrame(() => {
        frame = 0;
        if (window.matchMedia(MOBILE_QUERY).matches) return;
        if (window.scrollY > SCROLL_COLLAPSE_AT) setAutoCollapsed(true);
      });
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => {
      window.removeEventListener('scroll', onScroll);
      cancelAnimationFrame(frame);
    };
  }, [pathname]);

  const onContentInput = () => {
    if (menuCollapsed || window.matchMedia(MOBILE_QUERY).matches) return;
    setAutoCollapsed(true);
  };

  const expandMenu = () => {
    setCollapsed(false);
    setAutoCollapsed(false);
    setPeek(false);
  };

  const toggleMenu = () => {
    if (window.matchMedia(MOBILE_QUERY).matches) setMobileOpen(o => !o);
    else if (autoCollapsed) expandMenu();
    else setCollapsed(c => !c);
  };

  const toggleRef = useRef(toggleMenu);
  toggleRef.current = toggleMenu;

  // Ctrl+B (⌘+B trên Mac): thu gọn / mở rộng menu — bỏ qua khi đang gõ trong vùng soạn thảo định dạng. Gắn listener 1 lần.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!(e.ctrlKey || e.metaKey) || e.shiftKey || e.altKey || e.key.toLowerCase() !== 'b') return;
      if ((e.target as HTMLElement | null)?.isContentEditable) return;
      e.preventDefault();
      toggleRef.current();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className={cx(styles.shell, menuCollapsed && styles.collapsed)}>
      <Sidebar
        collapsed={menuCollapsed && !peek}
        peek={peek}
        mobileOpen={mobileOpen}
        onNavigate={() => setMobileOpen(false)}
        onExpand={expandMenu}
        onHover={hovering => setPeek(autoCollapsed && hovering)}
      />
      {mobileOpen && <div className={styles.scrim} onClick={() => setMobileOpen(false)} />}
      <div className={styles.main}>
        <Topbar onMenu={toggleMenu} menuCollapsed={menuCollapsed} onStartTour={tour.start} />
        <main className={cx(styles.content, FULL_SCREEN_PATHS.includes(pathname) && styles.fullScreen)} onInput={onContentInput}>
          <Suspense fallback={<p className={styles.loading}>{t('Đang tải…')}</p>}>
            <Outlet />
          </Suspense>
        </main>
        <ImportantNoticeModal />
        <OnboardingTour open={tour.open} onClose={tour.close} />
      </div>
    </div>
  );
}
