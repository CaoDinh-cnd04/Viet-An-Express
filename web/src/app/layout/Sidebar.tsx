import { NavLink, useLocation } from 'react-router-dom';
import { useCan } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useLocalStorage } from '@/shared/lib/useLocalStorage';
import { Icon } from '@/shared/ui';
import { NAV, type BadgeKey, type NavLinkItem } from './nav.config';
import { useNavBadges } from './useNavBadges';
import styles from './Sidebar.module.css';

interface SidebarProps {
  collapsed: boolean;
  /** Menu đang thu gọn tạm nhưng khách rê chuột vào: mở rộng đè lên nội dung, không đẩy trang. */
  peek?: boolean;
  mobileOpen: boolean;
  onNavigate: () => void;
  onExpand: () => void;
  onHover?: (hovering: boolean) => void;
}

export function Sidebar({ collapsed, peek, mobileOpen, onNavigate, onExpand, onHover }: SidebarProps) {
  const { pathname } = useLocation();
  const { t } = useI18n();
  const badges = useNavBadges();
  const allowed = useCan();
  const [openGroups, setOpenGroups] = useLocalStorage<Record<string, boolean>>('va.nav.groups', { create: true, orders: true });

  const isActive = (to: string) => pathname === to;
  const count = (key?: BadgeKey) => (key ? badges[key] : 0);

  return (
    <aside
      className={cx(styles.sidebar, collapsed && styles.collapsed, peek && styles.peek, mobileOpen && styles.mobileOpen)}
      aria-label={t('Menu chính')}
      onMouseEnter={() => onHover?.(true)}
      onMouseLeave={() => onHover?.(false)}
    >
      <div className={styles.brand}>
        <img src="/logo.webp" alt="" className={styles.logo} />
        <div className={styles.brandText}>
          <div className={styles.brandName}>Việt An Express</div>
        </div>
      </div>

      <nav className={styles.nav}>
        {NAV.map(entry => {
          if (entry.kind === 'link') {
            if (!allowed(entry.permission)) return null;
            return (
              <NavLink key={entry.to} to={entry.to} end onClick={onNavigate} className={({ isActive: a }) => cx(styles.item, styles.top, a && styles.active)} title={t(entry.label)} data-tour={`nav-${entry.to.slice(1)}`}>
                <Icon name={entry.icon} size={16} className={styles.icon} />
                <span className={styles.label}>{t(entry.label)}</span>
              </NavLink>
            );
          }

          const children = entry.children.filter(c => allowed(c.permission));
          if (children.length === 0) return null;
          const hasActive = children.some(c => isActive(c.to));
          const open = openGroups[entry.id] || hasActive;
          const groupCount = children.reduce((sum, c) => sum + count(c.badge), 0);
          return (
            <div key={entry.id} className={styles.group} data-tour={`nav-${entry.id}`}>
              <button
                type="button"
                className={cx(styles.item, styles.top, hasActive && styles.groupActive)}
                aria-expanded={open}
                title={t(entry.label)}
                onClick={() => {
                  if (collapsed) {
                    onExpand();
                    setOpenGroups(g => ({ ...g, [entry.id]: true }));
                  } else setOpenGroups(g => ({ ...g, [entry.id]: !open }));
                }}
              >
                <Icon name={entry.icon} size={16} className={styles.icon} />
                <span className={styles.label}>{t(entry.label)}</span>
                <span className={styles.short} aria-hidden="true">{t(entry.short)}</span>
                {!open && groupCount > 0 && <span className={styles.badge}>{groupCount}</span>}
                <Icon name={open ? 'chevronDown' : 'chevronRight'} size={14} className={styles.chevron} />
              </button>
              {open && !collapsed && (
                <div className={styles.children}>
                  {children.map(child => (
                    <ChildLink key={child.to} item={child} count={count(child.badge)} onNavigate={onNavigate} />
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </nav>
    </aside>
  );
}

function ChildLink({ item, count, onNavigate }: { item: NavLinkItem; count: number; onNavigate: () => void }) {
  const { t } = useI18n();
  return (
    <NavLink to={item.to} end onClick={onNavigate} className={({ isActive }) => cx(styles.item, styles.child, isActive && styles.active)}>
      <span className={styles.label}>{t(item.label)}</span>
      {count > 0 && <span className={cx(styles.badge, item.badge === 'troubles' && styles.badgeAlert)}>{count}</span>}
    </NavLink>
  );
}
