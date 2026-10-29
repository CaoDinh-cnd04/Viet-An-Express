import { useEffect, useId, useRef, useState } from 'react';
import { Controller, useFormContext } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Card, Icon } from '@/shared/ui';
import { useAddonFees } from '../api';
import { ADDONS } from '../constants';
import { addonFeeRows } from '../lib/addon-fees';
import { AddonFeesDialog } from './AddonFeesDialog';
import type { CreateOrderValues } from '@/features/order-form';
import styles from './form.module.css';

/** Nhãn khi không chọn dịch vụ nào (giá trị lưu là danh sách rỗng). */
const NO_ADDON = 'Không sử dụng dịch vụ';

/**
 * Tùy chọn dịch vụ — ô chọn xổ xuống, mặc định "Không sử dụng dịch vụ".
 * Danh sách: "Không sử dụng dịch vụ" (bỏ hết) + các dịch vụ chọn nhiều.
 * Giá trị lưu là tên tiếng Việt; hiển thị theo ngôn ngữ đang chọn.
 */
export function AddonsSection() {
  const { t } = useI18n();
  const { control } = useFormContext<CreateOrderValues>();
  const [showFees, setShowFees] = useState(false);
  return (
    <Card accent
      title="Tùy chọn dịch vụ"
      subtitle="(có tính phí)"
      actions={
        <button type="button" className={styles.linkButton} onClick={() => setShowFees(true)}>
          <Icon name="tag" size={13} /> {t('Xem biểu phí dịch vụ')}
        </button>
      }
    >
      <Controller
        control={control}
        name="addons"
        render={({ field }) => (
          <>
            <AddonPicker value={field.value} onChange={field.onChange} onShowFees={() => setShowFees(true)} />
            <AddonFeesDialog open={showFees} onClose={() => setShowFees(false)} selected={field.value} />
          </>
        )}
      />
    </Card>
  );
}

function AddonPicker({ value, onChange, onShowFees }: { value: string[]; onChange: (next: string[]) => void; onShowFees: () => void }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  // Phí từng dịch vụ (khi backend có) hiện ngay trong danh sách
  const fees = useAddonFees(open);
  const priceOf = new Map(addonFeeRows(ADDONS, fees.data).map(r => [r.name, r.price]));
  const root = useRef<HTMLDivElement>(null);
  const listId = useId();

  // Đóng khi bấm ra ngoài hoặc nhấn Esc
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => !root.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const none = value.length === 0;
  const toggle = (name: string) => onChange(value.includes(name) ? value.filter(x => x !== name) : [...value, name]);
  const selected = value.map(name => ADDONS.find(a => a.name === name) ?? { name, nameEn: name, description: '', descriptionEn: '' });

  return (
    <div ref={root} className={styles.addonPicker}>
      {/* Bấm vùng trống của ô để mở danh sách; mỗi dịch vụ đã chọn là 1 thẻ có nút × bỏ ngay, không cần mở danh sách */}
      <div className={cx(styles.addonTrigger, open && styles.addonTriggerOpen)} onClick={() => setOpen(o => !o)}>
        <div className={styles.addonChips}>
          {none ? (
            <span className={styles.addonNone}>{t(NO_ADDON)}</span>
          ) : (
            selected.map(a => (
              <span key={a.name} className={styles.addonChip}>
                {t(a.name)}
                <button
                  type="button"
                  className={styles.addonChipRemove}
                  aria-label={t('Bỏ dịch vụ {name}', { name: t(a.name) })}
                  onClick={e => {
                    e.stopPropagation();
                    toggle(a.name);
                  }}
                >
                  <Icon name="close" size={11} />
                </button>
              </span>
            ))
          )}
        </div>
        <button
          type="button"
          className={styles.addonToggle}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-controls={listId}
          aria-label={t('Chọn dịch vụ')}
          onClick={e => {
            e.stopPropagation();
            setOpen(o => !o);
          }}
        >
          <Icon name="chevronDown" size={15} className={cx(styles.chevron, open && styles.chevronOpen)} />
        </button>
      </div>

      {open && (
        <div id={listId} role="listbox" aria-multiselectable="true" aria-label={t('Tùy chọn dịch vụ')} className={styles.addonMenu}>
          <label className={cx(styles.addonOption, none && styles.addonOptionOn)}>
            <input type="radio" checked={none} onChange={() => onChange([])} />
            <span>
              <strong>{t(NO_ADDON)}</strong>
              <small>{t('Gửi hàng theo dịch vụ tiêu chuẩn, không thêm phí')}</small>
            </span>
          </label>
          <div className={styles.addonDivider} />
          {ADDONS.map(a => {
            const checked = value.includes(a.name);
            return (
              <label key={a.name} className={cx(styles.addonOption, checked && styles.addonOptionOn)}>
                <input type="checkbox" checked={checked} onChange={() => toggle(a.name)} />
                <span>
                  <strong>{t(a.name)}</strong>
                  <small>{t(a.description)}</small>
                </span>
                <span className={styles.addonPrice}>{priceOf.get(a.name) ? t(priceOf.get(a.name)!) : t('Có phí')}</span>
              </label>
            );
          })}
          <div className={styles.addonFooter}>
            <button type="button" className={styles.linkButton} onClick={() => { setOpen(false); onShowFees(); }}>
              <Icon name="tag" size={13} /> {t('Xem biểu phí dịch vụ')}
            </button>
            <button type="button" className={styles.linkButton} onClick={() => setOpen(false)}>{t('Xong')}</button>
          </div>
        </div>
      )}
    </div>
  );
}
