import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, Modal } from '@/shared/ui';
import { useMarkRead, useNotifications } from '../api';
import { partsOf } from '../lib/inbox';
import { NoticeContent } from './NoticeContent';
import styles from './notifications.module.css';

/** Tin đã bấm "Xem sau" trong phiên làm việc này — không hiện lại tới khi đóng tab; tin quan trọng mới đăng sau đó vẫn hiện. */
const LATER_KEY = 'va.importantNotice.later';
const readLater = (): string[] => {
  try {
    const v: unknown = JSON.parse(window.sessionStorage.getItem(LATER_KEY) ?? '[]');
    return Array.isArray(v) ? v.map(String) : [];
  } catch {
    return [];
  }
};
const saveLater = (ids: string[]) => {
  try {
    window.sessionStorage.setItem(LATER_KEY, JSON.stringify(ids));
  } catch {
    /* storage bị chặn: chỉ nhớ tới khi tải lại trang */
  }
};

/**
 * Popup tin quan trọng: hiện cùng lúc mọi tin quan trọng **chưa đọc** (dù đăng ở các lần khác nhau), xếp nối nhau mới nhất trước.
 * Tin đã đọc không hiện lại; admin đăng thêm tin quan trọng khi khách đang mở portal → popup hiện tin mới đó (danh sách tải lại mỗi 5 phút).
 * "Đã đọc tất cả" đánh dấu đã đọc mọi tin trong popup; "Xem sau" ẩn các tin đó trong phiên làm việc này.
 */
export function ImportantNoticeModal() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { data } = useNotifications();
  const markRead = useMarkRead();
  // Tin đã xử lý trong phiên (Xem sau / vừa bấm đã đọc — tránh hiện lại trước khi danh sách tải lại).
  const [handled, setHandled] = useState(readLater);
  // Danh sách chốt lúc mở: đánh dấu đã đọc không làm tin biến mất khỏi popup đang xem.
  const [ids, setIds] = useState<string[] | null>(null);

  const all = data?.data ?? [];
  const pending = all.filter(n => n.kind === 'notice' && n.imp && n.unread && !handled.includes(String(n.id)));
  if (!ids && pending.length) setIds(pending.map(n => String(n.id)));
  const shown = (ids ?? []).map(id => all.find(n => String(n.id) === id)).filter(n => !!n);

  const finish = (later: boolean) => {
    const done = [...new Set([...handled, ...(ids ?? [])])];
    if (later) saveLater([...new Set([...readLater(), ...(ids ?? [])])]);
    setHandled(done);
    setIds(null);
  };
  const close = () => finish(true);
  const readAll = () => {
    for (const n of shown) if (n.unread) markRead.mutate(n.id);
    finish(false);
  };

  return (
    <Modal
      open={shown.length > 0}
      title={shown.length > 1 ? t('Thông báo quan trọng ({n})', { n: shown.length }) : 'Thông báo quan trọng'}
      size="lg"
      onClose={close}
      footer={
        <>
          <Button variant="ghost" onClick={close}>{t('Xem sau')}</Button>
          <Button onClick={() => { close(); void navigate('/notifications'); }}>{t('Xem tất cả')}</Button>
          <Button variant="primary" onClick={readAll}>
            <Icon name="check" size={15} /> {t(shown.length > 1 ? 'Đã đọc tất cả' : 'Đã đọc')}
          </Button>
        </>
      }
    >
      <div className={styles.stack}>
        {shown.map(n => (
          <section key={n.id} className={styles.notice}>
            <NoticeContent title={n.title} meta={`${t('Việt An Express')} · ${n.date}`} parts={partsOf(n)} />
          </section>
        ))}
      </div>
    </Modal>
  );
}
