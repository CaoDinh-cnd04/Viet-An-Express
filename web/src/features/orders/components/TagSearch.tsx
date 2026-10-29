import { useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react';
import { useI18n } from '@/shared/i18n';
import { SEARCH_FIELDS } from '../constants';
import { addTags, MAX_TERMS, pastedValues, TAG_SEPARATOR, tokenize, type SearchTag } from '../lib/multi-filter';
import type { OrderSearchField } from '../types';
import styles from './TagSearch.module.css';

interface TagSearchProps {
  tags: readonly SearchTag[];
  onTagsChange: (tags: SearchTag[]) => void;
  /** Trường áp dụng cho thẻ sắp tạo. */
  field: OrderSearchField;
  onFieldChange: (field: OrderSearchField) => void;
}

/** Nhãn ngắn trên thẻ (trường "Tất cả" không ghi nhãn). */
const TAG_LABEL: Record<OrderSearchField, string> = {
  all: '',
  cnee: 'Người nhận',
  bill: 'Bill',
  ref: 'Ref',
  ct: 'Nước đến',
  svc: 'Dịch vụ'
};

/**
 * Ô tìm dạng thẻ: gõ từ khóa rồi bấm dấu phẩy hoặc Enter → thành thẻ có nút ×. Không phân biệt hoa / thường.
 * - Mỗi thẻ nhớ trường đang chọn lúc tạo (Người nhận, Nước đến…).
 * - Từ khóa được có dấu cách (vd tên người nhận "HONG AN DUONG").
 * - Dán cả cột từ Excel: mỗi dòng 1 thẻ. Backspace ở ô trống xóa thẻ cuối.
 */
export function TagSearch({ tags, onTagsChange, field, onFieldChange }: TagSearchProps) {
  const { t } = useI18n();
  const [text, setText] = useState('');
  const input = useRef<HTMLInputElement>(null);

  const commit = (raw: string) => {
    const values = tokenize(raw);
    setText('');
    if (values.length) onTagsChange(addTags(tags, field, values));
  };

  const remove = (i: number) => {
    onTagsChange(tags.filter((_, j) => j !== i));
    input.current?.focus();
  };

  const onInput = (value: string) => {
    // Gõ dấu phẩy (kể cả bàn phím điện thoại) → chốt thẻ.
    const last = value.slice(-1);
    if (TAG_SEPARATOR.test(last)) commit(value);
    else setText(value);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      commit(text);
    } else if (e.key === 'Backspace' && !text && tags.length) {
      remove(tags.length - 1);
    } else if (e.key === 'Escape') {
      setText('');
    }
  };

  const onPaste = (e: ClipboardEvent<HTMLInputElement>) => {
    const values = pastedValues(e.clipboardData.getData('text'));
    if (!values) return;
    e.preventDefault();
    onTagsChange(addTags(tags, field, values));
  };

  return (
    <div className={styles.wrap}>
      <div className={styles.group}>
        <select aria-label={t('Trường tìm kiếm')} className={styles.field} value={field} onChange={e => onFieldChange(e.target.value as OrderSearchField)}>
          {SEARCH_FIELDS.map(f => (
            <option key={f.value} value={f.value}>
              {t(f.label)}
            </option>
          ))}
        </select>
        <div className={styles.box} onClick={() => input.current?.focus()}>
          {tags.map((tag, i) => (
            <span key={`${tag.field}:${tag.value}`} className={styles.tag} data-field={tag.field}>
              {TAG_LABEL[tag.field] && <span className={styles.tagField}>{t(TAG_LABEL[tag.field])}:</span>}
              <span className={styles.tagValue}>{tag.value}</span>
              <button type="button" className={styles.tagRemove} aria-label={t('Bỏ từ khóa {value}', { value: tag.value })} onClick={() => remove(i)}>
                ×
              </button>
            </span>
          ))}
          <input
            ref={input}
            id="order-search"
            className={styles.input}
            placeholder={tags.length ? t('Thêm từ khóa…') : t('Gõ từ khóa rồi bấm dấu phẩy (,) hoặc Enter')}
            value={text}
            onChange={e => onInput(e.target.value)}
            onKeyDown={onKeyDown}
            onPaste={onPaste}
            onBlur={() => commit(text)}
            disabled={tags.length >= MAX_TERMS}
          />
        </div>
        {tags.length > 0 && (
          <button type="button" className={styles.clearAll} onClick={() => onTagsChange([])}>
            {t('Xóa hết')}
          </button>
        )}
      </div>
    </div>
  );
}
