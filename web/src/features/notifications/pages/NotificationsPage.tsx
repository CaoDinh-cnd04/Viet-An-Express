import { isNotImplemented } from '@/shared/api/http';
import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Card, EmptyState, ExternalLinkButton, Icon, Modal, PageHeader, StatusPill } from '@/shared/ui';
import { useMarkAllRead, useMarkRead, useNotifications, type Notification } from '../api';
import { safeLink } from '../lib/link';
import styles from '../components/notifications.module.css';

export default function NotificationsPage() {
  const { t } = useI18n();
  const { data, isLoading, isError, error, refetch } = useNotifications();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const [open, setOpen] = useState<Notification | null>(null);
  const items = data?.data ?? [];

  const openItem = (n: Notification) => {
    setOpen(n);
    if (n.unread) markRead.mutate(n.id);
  };

  return (
    <>
      <PageHeader
        title="Trung tâm thông báo"
        description="Thông báo từ Việt An: lịch cut-off, phụ phí, quy định hãng bay, bảo trì hệ thống."
        actions={
          <Button size="sm" onClick={() => markAll.mutate()} disabled={!data?.unreadCount || markAll.isPending}>
            {t('Đánh dấu đã đọc tất cả')}
          </Button>
        }
      />
      <Card flush>
        {isError && isNotImplemented(error) ? (
          <EmptyState title="Chức năng thông báo đang được kết nối máy chủ" description="Vui lòng thử lại sau." />
        ) : isError ? (
          <EmptyState title="Không tải được thông báo" action={<Button onClick={() => void refetch()}>{t('Thử lại')}</Button>} />
        ) : !isLoading && !items.length ? (
          <EmptyState title="Chưa có thông báo" />
        ) : (
          <ul className={styles.list}>
            {items.map(n => (
              <li key={n.id}>
                <button type="button" className={cx(styles.item, n.unread && styles.unread)} onClick={() => openItem(n)}>
                  <span className={styles.text}>
                    <span className={styles.title}>
                      {n.title}
                      {n.imp && <StatusPill tone="warning">{'Quan trọng'}</StatusPill>}
                    </span>
                    <span className={styles.snippet}>{n.body.split('\n')[0]}</span>
                    <span className={styles.date}>{n.date}</span>
                  </span>
                  {n.unread && <span className={styles.dot} aria-label={t('Chưa đọc')} />}
                </button>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Modal open={!!open} title={open?.title ?? ''} size="lg" onClose={() => setOpen(null)} footer={<Button onClick={() => setOpen(null)}>{t('Đóng')}</Button>}>
        {open && (
          <>
            <p className={styles.meta}>{open.date}</p>
            <div className={styles.body}>{open.body}</div>
            {safeLink(open.link) && (
              <div className={styles.linkRow}>
                <ExternalLinkButton variant="primary" href={safeLink(open.link)!}>
                  {t('Xem tại đây')} <Icon name="arrowRight" size={15} />
                </ExternalLinkButton>
              </div>
            )}
          </>
        )}
      </Modal>
    </>
  );
}
