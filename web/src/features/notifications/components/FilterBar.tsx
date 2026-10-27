import type { ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import styles from './FilterBar.module.css';

export interface FilterOption<K extends string> {
  key: K;
  label: string;
  count?: number;
  /** Số đỏ (chưa đọc) thay vì số xám. */
  hot?: boolean;
}

interface FilterBarProps<K extends string> {
  options: ReadonlyArray<FilterOption<K>>;
  value: K;
  onChange: (key: K) => void;
  query: string;
  onQuery: (q: string) => void;
  /** Chữ gợi ý ô tìm — mặc định "Tìm thông báo". */
  placeholder?: string;
  /** Ô lọc thêm đặt giữa nút lọc và ô tìm (VD chọn nhân viên). */
  children?: ReactNode;
}

/** Nút lọc liền khối có số đếm + ô tìm — dùng chung trang Thông báo của khách và trang quản trị. */
export function FilterBar<K extends string>({ options, value, onChange, query, onQuery, placeholder = 'Tìm thông báo', children }: FilterBarProps<K>) {
  const { t } = useI18n();
  return (
    <div className={styles.bar}>
      <div className={styles.seg} role="group" aria-label={t('Lọc thông báo')}>
        {options.map(o => (
          <button key={o.key} type="button" aria-pressed={value === o.key} className={cx(styles.option, value === o.key && styles.on)} onClick={() => onChange(o.key)}>
            {t(o.label)}
            {!!o.count && <span className={cx(styles.count, o.hot && styles.hot)}>{o.count}</span>}
          </button>
        ))}
      </div>
      {children}
      <label className={styles.search}>
        <Icon name="search" size={15} />
        <input value={query} onChange={e => onQuery(e.target.value)} placeholder={t(placeholder)} aria-label={t(placeholder)} />
      </label>
    </div>
  );
}
