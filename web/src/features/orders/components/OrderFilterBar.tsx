import { useEffect, useState, type ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { useDebouncedCallback } from '@/shared/lib/useDebouncedCallback';
import { Icon } from '@/shared/ui';
import { parseTags, serializeTags } from '../lib/multi-filter';
import type { OrderFilters } from '../types';
import { TagSearch } from './TagSearch';
import styles from './OrderFilterBar.module.css';

interface OrderFilterBarProps {
  filters: OrderFilters;
  onChange: (patch: Partial<OrderFilters>) => void;
  onReset: () => void;
  /** Nút thao tác đặt sau "Xóa lọc" (In bill, Xuất bảng kê). */
  actions?: ReactNode;
}

type TextKey = 'weightFrom' | 'weightTo';

export function OrderFilterBar({ filters, onChange, onReset, actions }: OrderFilterBarProps) {
  const { t } = useI18n();
  // Ô gõ tự do giữ state cục bộ để nhập mượt, đẩy lên URL sau khi ngừng gõ.
  const [draft, setDraft] = useState<Record<TextKey, string>>({ weightFrom: filters.weightFrom, weightTo: filters.weightTo });
  useEffect(() => setDraft({ weightFrom: filters.weightFrom, weightTo: filters.weightTo }), [filters.weightFrom, filters.weightTo]);
  const pushText = useDebouncedCallback((patch: Partial<OrderFilters>) => onChange(patch));

  // Đẩy cả 2 ô cân 1 lần: gõ "Cân từ" rồi "Cân đến" trong lúc chờ thì lần chờ sau không làm mất số của ô kia.
  const setWeight = (key: TextKey, value: string) => {
    const next = { ...draft, [key]: value };
    setDraft(next);
    pushText(next);
  };

  // Thẻ tìm lưu trên URL (?q=cnee:Uyen%0Act:Singapore); link cũ không có tiền tố dùng trường đang chọn.
  const tags = parseTags(filters.q, filters.searchField);

  return (
    <section className={styles.bar} aria-label={t('Bộ lọc đơn hàng')}>
      {/* Nhãn nằm bên trái ô — thanh lọc cao đúng 1 ô. data-tour: mốc cho hướng dẫn từng nút của trang (features/onboarding) */}
      <div className={styles.row} data-tour="orders-filters">
        <span className={styles.group}>
          <span className={styles.label}>{t('Ngày')}</span>
          <DateFilter label="Từ ngày" value={filters.fromDate} max={filters.toDate} onCommit={fromDate => onChange({ fromDate })} />
          <span className={styles.dash} aria-hidden>–</span>
          <DateFilter label="Đến ngày" value={filters.toDate} min={filters.fromDate} onCommit={toDate => onChange({ toDate })} />
        </span>
        <label className={styles.group}>
          <span className={styles.label}>{t('Loại')}</span>
          <select className={styles.select} value={filters.type} onChange={e => onChange({ type: e.target.value as OrderFilters['type'] })}>
            <option value="">{t('Tất cả')}</option>
            <option value="DOC">DOC</option>
            <option value="PACK">PACK</option>
          </select>
        </label>
        <span className={styles.group}>
          <span className={styles.label}>{t('Cân')}</span>
          <WeightFilter label="Cân từ" placeholder="từ" value={draft.weightFrom} onChange={v => setWeight('weightFrom', v)} />
          <span className={styles.dash} aria-hidden>–</span>
          <WeightFilter label="Cân đến" placeholder="đến" value={draft.weightTo} onChange={v => setWeight('weightTo', v)} />
        </span>
        <div className={styles.search} data-tour="orders-search">
          <label className="visually-hidden" htmlFor="order-search">{t('Tìm theo')}</label>
          <TagSearch
            tags={tags}
            onTagsChange={next => onChange({ q: serializeTags(next) })}
            field={filters.searchField}
            onFieldChange={searchField => onChange({ searchField, page: filters.page })}
          />
        </div>
        <div className={styles.tail}>
          {/* Nút chỉ icon (phễu gạch chéo), rê chuột hiện chú thích */}
          <button type="button" className={styles.clear} onClick={onReset} aria-label={t('Xóa lọc')} title={t('Xóa lọc')}>
            <Icon name="filterClear" size={15} />
          </button>
          <span className={styles.sep} aria-hidden />
          {actions}
        </div>
      </div>
    </section>
  );
}

/**
 * Ô ngày: giữ giá trị gõ dở trong ô, chỉ áp bộ lọc khi đã có đủ năm (4 chữ số, từ 1900) hoặc khi rời ô.
 * Trước đây áp ngay từng phím → gõ năm "2…" thành năm 0002, trang lọc lại và ô năm bị xoá, không gõ tiếp được.
 */
function DateFilter({ label, value, min, max, onCommit }: { label: string; value: string; min?: string; max?: string; onCommit: (v: string) => void }) {
  const { t } = useI18n();
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const complete = (v: string) => v === '' || (/^\d{4}-\d{2}-\d{2}$/.test(v) && Number(v.slice(0, 4)) >= 1900);
  const commit = (v: string) => {
    if (v !== value && complete(v)) onCommit(v);
  };
  return (
    <input
      className={styles.date}
      type="date"
      aria-label={t(label)}
      title={t(label)}
      min={min || undefined}
      max={max || undefined}
      value={text}
      onChange={e => {
        setText(e.target.value);
        commit(e.target.value);
      }}
      onBlur={e => commit(e.target.value)}
    />
  );
}

/** Ô cân (kg) có nút tăng / giảm bước 0.5 kg, gõ tay được số lẻ. Nhãn chung "Cân" bên trái cặp ô — từng ô đọc tên qua aria-label. */
function WeightFilter({ label, placeholder, value, onChange }: { label: string; placeholder: string; value: string; onChange: (v: string) => void }) {
  const { t } = useI18n();
  const step = (delta: number) => {
    const n = Math.max(0, Math.round(((Number(value) || 0) + delta) * 100) / 100);
    onChange(n ? String(n) : '');
  };
  return (
    <div className={styles.weightBox}>
      <input type="number" min={0} step={0.1} inputMode="decimal" aria-label={t(label)} title={t(label)} placeholder={t(placeholder)}
        value={value} onChange={e => onChange(e.target.value)} />
      <span className={styles.stepper}>
        <button type="button" aria-label={t('Tăng {label}', { label: t(label) })} onClick={() => step(0.5)}>
          <Icon name="chevronDown" size={11} className={styles.up} />
        </button>
        <button type="button" aria-label={t('Giảm {label}', { label: t(label) })} onClick={() => step(-0.5)}>
          <Icon name="chevronDown" size={11} />
        </button>
      </span>
    </div>
  );
}
