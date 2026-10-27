import { useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { DropdownMenu, Icon } from '@/shared/ui';
import { useMarkAllRead, useMarkRead, useNotifications, type Notification } from '../api';
import { bellItems } from '../lib/inbox';
import { NoticeRow } from './NoticeRow';
import styles from './NotificationBell.module.css';

/** Số thông báo hiện trong popup (chưa đọc trước); xem hết ở trang Thông báo. */
const POPUP_LIMIT = 6;

/**
 * Nút chuông trên thanh trên cùng: popup các thông báo mới nhất (cùng kiểu dòng với trang Thông báo),
 * bấm 1 thông báo → đánh dấu đã đọc và mở ở trang Thông báo; "Đã đọc hết" ngay trong popup.
 */
export function NotificationBell({ className, badgeClassName }: { className?: string; badgeClassName?: string }) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { data, isLoading, isError, error } = useNotifications();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const close = useRef<() => void>(() => undefined);
  const items = bellItems(data?.data ?? [], POPUP_LIMIT);
  const unread = data?.unreadCount ?? 0;
  const today = new Date();

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
          <button type="button" className={styles.markAll} onClick={() => markAll.mutate()} disabled={!unread || markAll.isPending}>
            <Icon name="check" size={13} /> {t('Đã đọc hết')}
          </button>
        </div>

        <div className={styles.list}>
          {isLoading && <p className={styles.note}>{t('Đang tải…')}</p>}
          {isError && <p className={styles.note}>{t(isNotImplemented(error) ? 'Chức năng thông báo đang được kết nối máy chủ' : 'Không tải được thông báo')}</p>}
          {!isLoading && !isError && !items.length && (
            <p className={styles.empty}>
              <Icon name="bell" size={24} />
              {t('Chưa có thông báo')}
            </p>
          )}
          {items.map(n => (
            <NoticeRow key={n.id} notice={n} today={today} compact onOpen={open} />
          ))}
        </div>

        <button type="button" className={styles.all} onClick={() => go('/notifications')}>
          {t('Xem tất cả thông báo')} <Icon name="arrowRight" size={14} />
        </button>
      </div>
    </DropdownMenu>
  );
}
