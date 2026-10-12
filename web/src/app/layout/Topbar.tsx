import { Link, useMatches, useNavigate } from 'react-router-dom';
import { initialsOf, PERMISSIONS, useCan, useLogout, useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { useTheme } from '@/shared/lib/useTheme';
import { DropdownMenu, Icon, type MenuItem } from '@/shared/ui';
import type { RouteHandle } from '../routes';
import { useNavBadges } from './useNavBadges';
import styles from './Topbar.module.css';

/** Mac dùng ⌘, máy khác dùng Ctrl cho phím tắt thu gọn menu. */
const MOD_KEY = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform) ? '⌘' : 'Ctrl';

export function Topbar({ onMenu, menuCollapsed, onStartTour }: { onMenu: () => void; menuCollapsed: boolean; onStartTour: () => void }) {
  const matches = useMatches();
  const title = [...matches].reverse().map(m => (m.handle as RouteHandle | undefined)?.title).find(Boolean) ?? '';
  const { theme, toggle } = useTheme();
  const { t, lang, setLang } = useI18n();
  const { notifications } = useNavBadges();
  const session = useSession();
  const logout = useLogout();
  const navigate = useNavigate();
  const allowed = useCan();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  // Tài khoản con: hiện tên nhân viên kèm tên công ty để biết đang dùng tài khoản nào.
  const accountLabel = user && (user.accountType === 'staff' ? `${user.fullName ?? user.userName ?? ''} · ${user.companyName}` : user.companyName);
  const initials = initialsOf(user?.companyName);

  const menu: MenuItem[] = [
    { label: 'Xem hướng dẫn sử dụng', onSelect: onStartTour },
    // Tài khoản nhân viên không có quyền tự đổi mật khẩu — admin đặt lại ở trang Tài khoản nhân viên.
    ...(allowed(PERMISSIONS.changePassword) ? [{ label: 'Đổi mật khẩu', onSelect: () => void navigate('/account/password') }] : []),
    ...(user && allowed(PERMISSIONS.manageStaff) ? [{ label: 'Tài khoản nhân viên', onSelect: () => void navigate('/account/staff') }] : []),
    { label: 'Trang giới thiệu', onSelect: () => void navigate('/') },
    ...(user
      ? [{ label: 'Đăng xuất', danger: true, onSelect: () => logout.mutate() }]
      : [])
  ];

  return (
    <header className={styles.topbar}>
      <button
        type="button"
        className={styles.navToggle}
        onClick={onMenu}
        aria-label={t(menuCollapsed ? 'Mở rộng menu' : 'Thu gọn menu')}
        aria-keyshortcuts="Control+B"
      >
        <Icon name="sidebar" size={18} />
        <span className={styles.tooltip} role="tooltip">
          {t(menuCollapsed ? 'Mở rộng menu' : 'Thu gọn menu')}
          <kbd>{MOD_KEY}</kbd>
          <kbd>B</kbd>
        </span>
      </button>
      <nav className={styles.crumb} aria-label="Breadcrumb">
        <span>{t('Portal khách hàng')}</span>
        {title && <><span aria-hidden="true">/</span><strong>{t(title)}</strong></>}
      </nav>
      <div className={styles.right}>
        <div className={styles.lang} role="group" aria-label={t('Ngôn ngữ')}>
          {(['vi', 'en'] as const).map(l => (
            <button key={l} type="button" className={l === lang ? styles.langOn : undefined} aria-pressed={l === lang} onClick={() => setLang(l)}>
              {l.toUpperCase()}
            </button>
          ))}
        </div>
        <button type="button" className={styles.iconBtn} onClick={toggle} aria-label={t(theme === 'dark' ? 'Chuyển giao diện sáng' : 'Chuyển giao diện tối')}>
          <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={16} />
        </button>
        <Link to="/notifications" className={styles.iconBtn} data-tour="notifications" aria-label={notifications ? t('Thông báo, {n} chưa đọc', { n: notifications }) : t('Thông báo')}>
          <Icon name="bell" size={16} />
          {notifications > 0 && <span className={styles.dot}>{notifications}</span>}
        </Link>
        <DropdownMenu
          items={menu}
          trigger={({ open, toggle }) => (
            <button type="button" className={styles.avatar} data-tour="account" onClick={toggle} aria-expanded={open} aria-haspopup="menu" aria-label={t('Tài khoản')} title={accountLabel}>
              {initials || <Icon name="user" size={16} />}
            </button>
          )}
        />
      </div>
    </header>
  );
}
