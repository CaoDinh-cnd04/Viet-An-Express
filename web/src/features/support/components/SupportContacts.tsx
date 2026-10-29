import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { CONTACTS, FAQ } from '../constants';
import styles from './SupportContacts.module.css';

/** Liên hệ nhanh (hotline, email, giờ làm việc) + câu hỏi thường gặp — mỗi lần mở 1 câu để khung luôn gọn. */
export function SupportContacts() {
  const { t } = useI18n();
  const [open, setOpen] = useState<number | null>(null);
  return (
    <div className={styles.box}>
      <dl className={styles.kv}>
        {CONTACTS.map(c => (
          <div key={c.label}>
            <dt>{t(c.label)}</dt>
            <dd>{c.href ? <a href={c.href}>{c.value}</a> : t(c.value)}</dd>
          </div>
        ))}
      </dl>
      <h4 className={styles.title}>{t('Câu hỏi thường gặp')}</h4>
      <ul className={styles.faq}>
        {FAQ.map((f, i) => (
          <li key={f.q}>
            <button type="button" className={cx(styles.q, open === i && styles.on)} aria-expanded={open === i} onClick={() => setOpen(open === i ? null : i)}>
              {t(f.q)}
            </button>
            {open === i && <p className={styles.a}>{t(f.a)}</p>}
          </li>
        ))}
      </ul>
    </div>
  );
}
