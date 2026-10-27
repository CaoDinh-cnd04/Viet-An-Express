import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { notificationImageUrl, type Notification } from '../api';
import { excerpt, imagesOf, partsOf, shortTime } from '../lib/inbox';
import styles from './NoticeRow.module.css';

interface NoticeRowProps {
  notice: Notification;
  today: Date;
  active?: boolean;
  /** Popup chuông: không ảnh thu nhỏ, tóm tắt 2 dòng. */
  compact?: boolean;
  onOpen: (n: Notification) => void;
}

/** 1 dòng thông báo: chấm xanh = chưa đọc, vạch cam bên trái = quan trọng, nhãn Ghim / số tin, ảnh thu nhỏ, giờ. */
export function NoticeRow({ notice: n, today, active, compact, onOpen }: NoticeRowProps) {
  const { t } = useI18n();
  const parts = partsOf(n).length;
  const thumb = compact ? undefined : imagesOf(n)[0];
  return (
    <button
      type="button"
      className={cx(styles.row, n.unread && styles.unread, n.imp && styles.imp, active && styles.active, compact && styles.compact)}
      onClick={() => onOpen(n)}
      aria-current={active || undefined}
    >
      {n.unread && <span className={styles.dot} aria-label={t('Chưa đọc')} />}
      <span className={styles.text}>
        <span className={styles.title}>
          <span className={styles.titleText}>{n.title}</span>
          {n.pin && n.kind === 'notice' && <span className={cx(styles.chip, styles.chipPin)}>{t('Ghim')}</span>}
          {n.imp && <span className={cx(styles.chip, styles.chipImp)}>{t('Quan trọng')}</span>}
          {parts > 1 && <span className={styles.chip}>{t('{n} tin', { n: parts })}</span>}
        </span>
        <span className={styles.snippet}>{excerpt(n)}</span>
      </span>
      <span className={styles.side}>
        {thumb && <img className={styles.thumb} src={notificationImageUrl(thumb)} alt="" loading="lazy" />}
        <span className={styles.time}>{shortTime(n.date, today)}</span>
      </span>
    </button>
  );
}
