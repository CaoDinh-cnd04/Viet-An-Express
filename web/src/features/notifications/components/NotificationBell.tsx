import { useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { DropdownMenu, Icon } from '@/shared/ui';
import { useMarkAllRead, useMarkRead, useNotifications, type Notification } from '../api';
import styles from './NotificationBell.module.css';

/** Số thông báo mới nhất hiện trong popup; xem hết ở trang Thông báo. */
const POPUP_LIMIT = 8;

/**
 * Nút chuông trên thanh trên cùng: bấm mở popup danh sách thông báo mới nhất (chưa đọc tô nền, nhãn Quan trọng),
 * bấm 1 thông báo → đánh dấu đã đọc và mở chi tiết ở trang Thông báo; "Đánh dấu đã đọc tất cả" ngay trong popup.
 */
export function NotificationBell({ className, badgeClassName }: { className?: string; badgeClassName?: string }) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { data, isLoading, isError, error } = useNotifications();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const close = useRef<() => void>(() => undefined);
  const items = (data?.data ?? []).slice(0, POPUP_LIMIT);
  const unread = data?.unreadCount ?? 0;

  const go = (path: string) => {
    close.current();
    void navigate(path);
  };
  const open = (n: Notification) => {
    if (n.unread) markRead.mutate(n.id);
    go(`/notifications?id=${encodeURIComponent(String(n.id))}`);
  };

  return (
    <DropdownMenu
      trigger={({ open: isOpen, toggle }) => {
        close.current = () => isOpen && toggle();
        return (
          <button
            type="button"
            className={className}
            data-tour="notifications"
            onClick={toggle}
            aria-expanded={isOpen}
            aria-haspopup="dialog"
            aria-label={unread ? t('Thông báo, {n} chưa đọc', { n: unread }) : t('Thông báo')}
          >
            <Icon name="bell" size={16} />
            {unread > 0 && <span className={badgeClassName}>{unread}</span>}
          </button>
        );
      }}
    >
      <div className={styles.pop}>
        <div className={styles.head}>
          <strong>{t('Thông báo')}</strong>
          {unread > 0 && <span className={styles.count}>{t('{n} chưa đọc', { n: unread })}</span>}
          <button type="button" className={styles.markAll} onClick={() => markAll.mutate()} disabled={!unread || markAll.isPending}>
            <Icon name="check" size={13} /> {t('Đánh dấu đã đọc tất cả')}
          </button>
        </div>

        <ul className={styles.list}>
          {isLoading && <li className={styles.note}>{t('Đang tải…')}</li>}
          {isError && <li className={styles.note}>{t(isNotImplemented(error) ? 'Chức năng thông báo đang được kết nối máy chủ' : 'Không tải được thông báo')}</li>}
          {!isLoading && !isError && !items.length && (
            <li className={styles.empty}>
              <Icon name="bell" size={26} />
              {t('Chưa có thông báo')}
            </li>
          )}
          {items.map(n => (
            <li key={n.id}>
              <button type="button" className={cx(styles.item, n.unread && styles.unread)} onClick={() => open(n)}>
                <span className={cx(styles.icon, n.imp && styles.iconImp)} aria-hidden="true">
                  <Icon name={n.imp ? 'alert' : 'bell'} size={15} />
                </span>
                <span className={styles.text}>
                  <span className={styles.title}>
                    {n.title}
                    {n.imp && <span className={styles.imp}>{t('Quan trọng')}</span>}
                  </span>
                  <span className={styles.snippet}>{n.body.split('\n')[0]}</span>
                  <span className={styles.date}>{n.date}</span>
                </span>
                {n.unread && <span className={styles.dot} aria-label={t('Chưa đọc')} />}
              </button>
            </li>
          ))}
        </ul>

        <button type="button" className={styles.all} onClick={() => go('/notifications')}>
          {t('Xem tất cả thông báo')} <Icon name="arrowRight" size={14} />
        </button>
      </div>
    </DropdownMenu>
  );
}
