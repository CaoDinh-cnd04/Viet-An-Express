import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { historyOf } from '../lib/history';
import type { SupportTicket } from '../types';
import styles from './SupportThreadList.module.css';

/**
 * Lịch sử trao đổi của 1 yêu cầu, mới nhất trước. `viewer` = ai đang xem: khách thấy "Bạn" / "CS Việt An", CS thấy "Khách" / "CS".
 */
export function SupportThreadList({ ticket, viewer }: { ticket: SupportTicket; viewer: 'customer' | 'cs' }) {
  const { t } = useI18n();
  const who = {
    sent: viewer === 'customer' ? 'Bạn gửi yêu cầu' : 'Khách gửi yêu cầu',
    cs: viewer === 'customer' ? 'CS Việt An' : 'CS trả lời',
    customer: viewer === 'customer' ? 'Bạn trả lời' : 'Khách trả lời'
  };
  return (
    <ul className={styles.log}>
      {historyOf(ticket).map((h, i) => (
        <li key={i}>
          <span className={styles.when}>{h.at}</span>
          {' · '}
          <b className={cx(h.kind === 'cs' && styles.cs)}>{t(who[h.kind])}</b>
          {h.kind !== 'sent' && <div className={styles.text}>{h.text}</div>}
        </li>
      ))}
    </ul>
  );
}
