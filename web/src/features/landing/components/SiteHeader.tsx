import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { PORTAL_HOME, useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useTheme } from '@/shared/lib/useTheme';
import { Button, Icon, LinkButton } from '@/shared/ui';
import { COMPANY, CONTACTS, NAV } from '../constants';
import styles from './SiteHeader.module.css';

interface SiteHeaderProps {
  /** Mở tab đăng nhập ở khung đầu trang. */
  onLogin: () => void;
}

/** Thanh điều hướng trang ngoài: trong suốt trên hero, có nền khi cuộn xuống. */
export function SiteHeader({ onLogin }: SiteHeaderProps) {
  const { t, lang, setLang } = useI18n();
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  const session = useSession();
  const { theme, toggle } = useTheme();
  const loggedIn = session.data?.status === 'authenticated';
  // Trang khác trang chủ (vd /tracking): link mục trỏ về trang chủ.
  const { pathname } = useLocation();
  const sectionHref = (id: string) => (pathname === '/' || pathname === '/login' ? `#${id}` : `/#${id}`);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  // Menu di động: Esc để đóng.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open]);

  const login = () => {
    setOpen(false);
    onLogin();
  };

  return (
    <header className={cx(styles.header, (scrolled || open) && styles.solid)}>
      <div className={styles.inner}>
        <Link to="/" className={styles.brand} onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}>
          <img src="/logo.webp" alt="" width={36} height={36} />
          <span>{COMPANY.name}</span>
        </Link>

        <nav id="site-nav" className={cx(styles.nav, open && styles.navOpen)} aria-label={t('Điều hướng chính')}>
          {NAV.map(item => (
            <a key={item.id} href={sectionHref(item.id)} onClick={() => setOpen(false)}>
              {t(item.label)}
            </a>
          ))}
          <a href={CONTACTS.hotline.href} className={styles.navHotline}>
            <Icon name="phone" size={16} /> {CONTACTS.hotline.label}
          </a>
        </nav>

        <div className={styles.actions}>
          <a href={CONTACTS.hotline.href} className={styles.hotline}>
            <Icon name="phone" size={16} />
            <span>{CONTACTS.hotline.label}</span>
          </a>
          <div className={styles.lang} role="group" aria-label={t('Ngôn ngữ')}>
            {(['vi', 'en'] as const).map(l => (
              <button key={l} type="button" className={l === lang ? styles.langOn : undefined} aria-pressed={l === lang} onClick={() => setLang(l)}>
                {l.toUpperCase()}
              </button>
            ))}
          </div>
          <Button
            iconOnly
            size="sm"
            variant="ghost"
            className={styles.themeButton}
            onClick={toggle}
            aria-label={t(theme === 'dark' ? 'Chuyển giao diện sáng' : 'Chuyển giao diện tối')}
            title={t(theme === 'dark' ? 'Giao diện sáng' : 'Giao diện tối')}
          >
            <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={18} />
          </Button>
          {loggedIn ? (
            <LinkButton to={PORTAL_HOME} variant="primary" size="sm" className={styles.cta}>
              {t('Vào portal')}
            </LinkButton>
          ) : (
            <Button variant="primary" size="sm" className={styles.cta} onClick={login}>
              {t('Đăng nhập')}
            </Button>
          )}
          <Button
            iconOnly
            size="sm"
            variant="ghost"
            className={styles.menuButton}
            aria-label={t(open ? 'Đóng menu' : 'Mở menu')}
            aria-expanded={open}
            aria-controls="site-nav"
            onClick={() => setOpen(o => !o)}
          >
            <Icon name={open ? 'close' : 'menu'} size={20} />
          </Button>
        </div>
      </div>
    </header>
  );
}
