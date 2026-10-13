import { useEffect, useId, useState, type ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { useDebouncedCallback } from '@/shared/lib/useDebouncedCallback';
import { Button, Icon, SelectField, TextField } from '@/shared/ui';
import { parseTags, serializeTags } from '../lib/multi-filter';
import type { OrderFilters } from '../types';
import { TagSearch } from './TagSearch';
import styles from './OrderFilterBar.module.css';

interface OrderFilterBarProps {
  filters: OrderFilters;
  onChange: (patch: Partial<OrderFilters>) => void;
  onReset: () => void;
  /** Nút thao tác đặt sau "Xóa lọc" (In nhiều bill, Xuất bảng kê). */
  actions?: ReactNode;
}

type TextKey = 'weightFrom' | 'weightTo';

export function OrderFilterBar({ filters, onChange, onReset, actions }: OrderFilterBarProps) {
  const { t } = useI18n();
  // Ô gõ tự do giữ state cục bộ để nhập mượt, đẩy lên URL sau khi ngừng gõ.
  const [draft, setDraft] = useState<Record<TextKey, string>>({ weightFrom: filters.weightFrom, weightTo: filters.weightTo });
  useEffect(() => setDraft({ weightFrom: filters.weightFrom, weightTo: filters.weightTo }), [filters.weightFrom, filters.weightTo]);
  const pushText = useDebouncedCallback((patch: Partial<OrderFilters>) => onChange(patch));

  const setWeight = (key: TextKey, value: string) => {
    setDraft(d => ({ ...d, [key]: value }));
    pushText({ [key]: value });
  };

  // Thẻ tìm lưu trên URL (?q=cnee:Uyen%0Act:Singapore); link cũ không có tiền tố dùng trường đang chọn.
  const tags = parseTags(filters.q, filters.searchField);

  return (
    <section className={styles.bar} aria-label={t('Bộ lọc đơn hàng')}>
      <div className={styles.row}>
        <DateFilter label="Từ ngày" value={filters.fromDate} max={filters.toDate} onCommit={fromDate => onChange({ fromDate })} />
        <DateFilter label="Đến ngày" value={filters.toDate} min={filters.fromDate} onCommit={toDate => onChange({ toDate })} />
        <SelectField
          label="Loại hàng"
          value={filters.type}
          onChange={e => onChange({ type: e.target.value as OrderFilters['type'] })}
          placeholder="Tất cả"
          options={['DOC', 'PACK']}
        />
        <WeightFilter label="Cân từ" value={draft.weightFrom} onChange={v => setWeight('weightFrom', v)} />
        <WeightFilter label="Cân đến" value={draft.weightTo} onChange={v => setWeight('weightTo', v)} />
        {/* Tìm theo cùng hàng với các ô lọc */}
        <div className={styles.search}>
          <label className={styles.searchLabel} htmlFor="order-search">{t('Tìm theo')}</label>
          <TagSearch
            tags={tags}
            onTagsChange={next => onChange({ q: serializeTags(next) })}
            field={filters.searchField}
            onFieldChange={searchField => onChange({ searchField, page: filters.page })}
          />
        </div>
        <div className={styles.tail}>
          <Button size="sm" onClick={onReset}>{t('Xóa lọc')}</Button>
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
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const complete = (v: string) => v === '' || (/^\d{4}-\d{2}-\d{2}$/.test(v) && Number(v.slice(0, 4)) >= 1900);
  const commit = (v: string) => {
    if (v !== value && complete(v)) onCommit(v);
  };
  return (
    <TextField
      label={label}
      type="date"
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

/** Ô cân (kg) có nút tăng / giảm bước 0.5 kg, gõ tay được số lẻ. */
function WeightFilter({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  const { t } = useI18n();
  const id = useId();
  const step = (delta: number) => {
    const n = Math.max(0, Math.round(((Number(value) || 0) + delta) * 100) / 100);
    onChange(n ? String(n) : '');
  };
  return (
    <div className={styles.weight}>
      <label className={styles.weightLabel} htmlFor={id} title={t(label)}>{t(label)}</label>
      <div className={styles.weightBox}>
        <input id={id} type="number" min={0} step={0.1} inputMode="decimal" value={value} onChange={e => onChange(e.target.value)} />
        <span className={styles.stepper}>
          <button type="button" aria-label={t('Tăng {label}', { label: t(label) })} onClick={() => step(0.5)}>
            <Icon name="chevronDown" size={11} className={styles.up} />
          </button>
          <button type="button" aria-label={t('Giảm {label}', { label: t(label) })} onClick={() => step(-0.5)}>
            <Icon name="chevronDown" size={11} />
          </button>
        </span>
      </div>
    </div>
  );
}
