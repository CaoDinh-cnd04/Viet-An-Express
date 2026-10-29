import { useEffect, useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { OTHER_UNIT, PRESET_UNITS, UNIT_MAX, unitChoice } from '@/features/order-form';
import type { CreateOrderValues } from '@/features/order-form';
import styles from './form.module.css';

/**
 * Ô ĐVT của 1 mặt hàng invoice: chọn đơn vị có sẵn (mặc định PCS); chọn "Khác" thì hiện ô để khách tự nhập.
 * Giá trị lưu vào `invoice.items.<i>.unit` luôn là chữ ĐVT thật (vd "PCS", "Đôi").
 */
export function UnitCell({ index, error }: { index: number; error?: string }) {
  const { t } = useI18n();
  const { control, register, setValue, getValues, trigger } = useFormContext<CreateOrderValues>();
  const name = `invoice.items.${index}.unit` as const;
  const unit = useWatch({ control, name }) ?? '';
  const [custom, setCustom] = useState(() => unitChoice(unit) === OTHER_UNIT);

  // Dữ liệu nạp từ ngoài (nhân bản đơn, nhập file) có ĐVT lạ → mở ô tự nhập.
  useEffect(() => {
    if (unitChoice(unit) === OTHER_UNIT) setCustom(true);
  }, [unit]);

  const choose = (value: string) => {
    const other = value === OTHER_UNIT;
    setCustom(other);
    setValue(name, other ? '' : value, { shouldDirty: true, shouldValidate: !!error });
    // Đổi ĐVT → kiểm tra lại số lượng (ĐVT có sẵn chỉ nhận số nguyên).
    if (getValues(`invoice.items.${index}.qty`)) void trigger(`invoice.items.${index}.qty`);
  };

  return (
    <>
      <select
        className={styles.cell}
        aria-label={t('Đơn vị mặt hàng {n}', { n: index + 1 })}
        value={custom ? OTHER_UNIT : unitChoice(unit)}
        onChange={e => choose(e.target.value)}
      >
        {PRESET_UNITS.map(u => (
          <option key={u} value={u}>
            {u}
          </option>
        ))}
        <option value={OTHER_UNIT}>{t(OTHER_UNIT)}</option>
      </select>
      {custom && (
        <input
          className={cx(styles.cell, styles.unitCustom, error && styles.cellInvalid)}
          placeholder={t('Nhập ĐVT (vd: Đôi, KG)')}
          aria-label={t('ĐVT khác của mặt hàng {n}', { n: index + 1 })}
          title={error}
          maxLength={UNIT_MAX}
          autoFocus={!unit}
          {...register(name)}
        />
      )}
    </>
  );
}
