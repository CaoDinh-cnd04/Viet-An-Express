import type { ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { Icon } from '@/shared/ui';
import styles from './SupportLayout.module.css';

/**
 * Bố cục chung của trang Hỗ trợ & góp ý — khách (SupportPage) và nhân viên Việt An (admin/ManagedSupport) dùng chung
 * để 2 giao diện luôn đồng bộ: khung trang, đầu trang, hàng lọc, 2 khung danh sách | chi tiết.
 */
export function SupportShell({ children }: { children: ReactNode }) {
  return <div className={styles.page}>{children}</div>;
}

/** Đầu trang: tiêu đề + mô tả ngắn bên trái, nút thao tác bên phải. */
export function SupportHeader({ title, sub, children }: { title: string; sub: string; children?: ReactNode }) {
  const { t } = useI18n();
  return (
    <header className={styles.head}>
      <div>
        <h1>{t(title)}</h1>
        <p>{t(sub)}</p>
      </div>
      {children && <div className={styles.actions}>{children}</div>}
    </header>
  );
}

/** Hotline góc trên: "Cần gấp? Gọi ngay" + số. */
export function SupportHotline({ href, value }: { href?: string; value: string }) {
  const { t } = useI18n();
  return (
    <a className={styles.hotline} href={href}>
      <Icon name="phone" size={14} />
      <span><small>{t('Cần gấp? Gọi ngay')}</small>{value}</span>
    </a>
  );
}

interface FilterSelectProps<V extends string> {
  label: string;
  value: V;
  options: ReadonlyArray<{ value: V; label: string }>;
  onChange: (value: V) => void;
}

/** Ô chọn trong hàng lọc: nhãn mờ bên trái + giá trị đậm (vd "Ngày  30 ngày"). Nhãn lựa chọn đã dịch sẵn hoặc là khóa dịch. */
export function FilterSelect<V extends string>({ label, value, options, onChange }: FilterSelectProps<V>) {
  const { t } = useI18n();
  return (
    <label className={styles.group}>
      <span>{t(label)}</span>
      <select value={value} onChange={e => onChange(e.target.value as V)}>
        {options.map(o => <option key={o.value} value={o.value}>{t(o.label)}</option>)}
      </select>
    </label>
  );
}

interface SupportFilterBarProps {
  query: string;
  onQuery: (q: string) => void;
  placeholder: string;
  /** Có lọc nào đang bật → nút "Đặt lại" bấm được. */
  canReset: boolean;
  onReset: () => void;
  /** Các FilterSelect. */
  children?: ReactNode;
}

/** Hàng lọc: ô tìm rộng · các ô chọn · | Đặt lại. */
export function SupportFilterBar({ query, onQuery, placeholder, canReset, onReset, children }: SupportFilterBarProps) {
  const { t } = useI18n();
  return (
    <div className={styles.filters}>
      <label className={styles.search}>
        <Icon name="search" size={14} />
        <input value={query} onChange={e => onQuery(e.target.value)} placeholder={t(placeholder)} aria-label={t('Tìm yêu cầu')} />
      </label>
      {children}
      <button type="button" className={styles.clear} onClick={onReset} disabled={!canReset}>
        <Icon name="refresh" size={14} /> {t('Đặt lại')}
      </button>
    </div>
  );
}

/** Thân trang: 2 khung tách rời — danh sách | chi tiết. */
export function SupportSplit({ list, pane, tour }: { list: ReactNode; pane: ReactNode; tour?: string }) {
  return (
    <section className={styles.split} data-tour={tour}>
      <div>{list}</div>
      <div>{pane}</div>
    </section>
  );
}
