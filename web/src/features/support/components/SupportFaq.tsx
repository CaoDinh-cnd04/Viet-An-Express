import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import { FAQ, FAQ_CATEGORIES } from '../constants';
import { searchFaq, type FaqCategory, type FaqItem } from '../lib/faq';
import styles from './SupportFaq.module.css';

/** 1 câu hỏi mở / đóng được — dùng ở khung Câu hỏi thường gặp và gợi ý trong form gửi yêu cầu. */
export function FaqEntry({ item, compact }: { item: FaqItem; compact?: boolean }) {
  const { t } = useI18n();
  return (
    <details className={cx(styles.qa, compact && styles.compact)}>
      <summary>{t(item.q)}</summary>
      <p>
        {t(item.a)}
        {item.link && (
          <>
            {' '}
            <Link to={item.link.to}>{t(item.link.label)}</Link>
          </>
        )}
      </p>
    </details>
  );
}

/** Khung Câu hỏi thường gặp (bên phải trang khi chưa chọn yêu cầu): tìm + lọc nhóm, cuối khung mời gửi yêu cầu. */
export function SupportFaq({ onClose, onNew }: { onClose?: () => void; onNew: () => void }) {
  const { t } = useI18n();
  const [cat, setCat] = useState<FaqCategory | ''>('');
  const [query, setQuery] = useState('');
  const items = searchFaq(FAQ.map(f => ({ ...f, q: t(f.q), a: t(f.a) })), cat, query);

  return (
    <div className={styles.faq}>
      <header className={styles.head}>
        <h3>{t('Câu hỏi thường gặp')}</h3>
        {onClose && (
          <button type="button" className={styles.close} onClick={onClose} aria-label={t('Đóng')}>
            <Icon name="close" size={14} />
          </button>
        )}
      </header>
      <div className={styles.body}>
        <label className={styles.search}>
          <Icon name="search" size={13} />
          <input value={query} onChange={e => setQuery(e.target.value)} placeholder={t('Tìm câu hỏi')} aria-label={t('Tìm câu hỏi')} />
        </label>
        <div className={styles.cats}>
          {FAQ_CATEGORIES.map(c => (
            <button key={c.value} type="button" className={cx(cat === c.value && styles.on)} aria-pressed={cat === c.value} onClick={() => setCat(c.value)}>
              {t(c.label)}
            </button>
          ))}
        </div>
        {items.length ? items.map(f => <FaqEntry key={f.q} item={f} />) : <p className={styles.empty}>{t('Không có câu hỏi phù hợp.')}</p>}
      </div>
      <p className={styles.foot}>
        {t('Chưa tìm thấy câu trả lời bạn cần?')}{' '}
        <button type="button" onClick={onNew}>{t('Gửi yêu cầu cho CS')}</button>
      </p>
    </div>
  );
}
