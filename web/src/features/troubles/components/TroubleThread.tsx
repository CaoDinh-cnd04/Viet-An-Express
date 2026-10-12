import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { TROUBLE_STATUS } from '../constants';
import type { TroubleMessage } from '../types';
import styles from './troubles.module.css';

/**
 * Lịch sử trao đổi khách ↔ Việt An của 1 sự cố (cũ trước, mới sau). Lượt nào đổi trạng thái thì ghi kèm.
 * `side`: phía đang xem — tin của phía mình nằm bên phải.
 */
export function TroubleThread({ messages, side }: { messages: TroubleMessage[]; side: 'customer' | 'vietan' }) {
  const { t } = useI18n();
  if (!messages.length) return <p className={styles.muted}>{t('Chưa có trao đổi.')}</p>;
  return (
    <ol className={styles.thread}>
      {messages.map((m, i) => (
        <li key={i} className={cx(styles.bubble, m.from === side && styles.mine, m.from === 'vietan' && styles.fromVietAn)}>
          <div className={styles.bubbleHead}>
            <strong>{m.from === 'vietan' ? t('CS Việt An') : m.author}</strong>
            <span>{m.date}</span>
          </div>
          {m.message && <p className={styles.bubbleText}>{m.message}</p>}
          {m.status && (
            <p className={styles.bubbleStatus}>
              {t('Chuyển trạng thái: {status}', { status: t(TROUBLE_STATUS[m.status]?.label ?? m.status) })}
            </p>
          )}
        </li>
      ))}
    </ol>
  );
}
