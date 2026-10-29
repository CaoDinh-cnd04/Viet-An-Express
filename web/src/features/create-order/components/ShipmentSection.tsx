import { Controller, useFormContext, useWatch } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { Card, FormGrid, SegmentedControl, TextField } from '@/shared/ui';
import { RULES } from '@/features/order-form';
import { useFieldBinder } from '../hooks/useFieldBinder';
import type { CreateOrderValues } from '@/features/order-form';
import styles from './form.module.css';

interface ShipmentSectionProps {
  onShipmentInput: () => void;
  /** Khách tự bấm chọn loại hàng. */
  onTypeChange: () => void;
  /** PACK hiện tại do hệ thống tự chuyển từ chứng từ quá cân. */
  docConverted: boolean;
}

const TYPE_OPTIONS = [
  { value: 'DOC', label: 'Chứng từ (DOC)' },
  { value: 'PACK', label: 'Hàng hóa (PACK)' }
] as const;

export function ShipmentSection({ onShipmentInput, onTypeChange, docConverted }: ShipmentSectionProps) {
  const bind = useFieldBinder();
  const { t, lang } = useI18n();
  const { control } = useFormContext<CreateOrderValues>();
  const type = useWatch({ control, name: 'shipment.type' });

  return (
    <Card accent title="Thông tin đơn hàng" subtitle={lang === 'vi' ? '(Info shipment)' : undefined}>
      <div className={styles.typeRow}>
        <span className={styles.typeLabel}>{t('Loại hàng')} <span className={styles.required}>*</span></span>
        <Controller
          control={control}
          name="shipment.type"
          render={({ field }) => (
            <SegmentedControl ariaLabel="Loại hàng" options={TYPE_OPTIONS} value={field.value} onChange={v => { field.onChange(v); onTypeChange(); }} />
          )}
        />
      </div>
      {type === 'DOC' ? (
        <>
          {/* Chứng từ: chỉ khai số kiện + cân nặng, nội dung mặc định Documents */}
          <FormGrid>
            <TextField label="Số kiện" required type="number" min={1} step={1} suffix={t('kiện')} {...bind('shipment.pieces', { onChange: onShipmentInput })} />
            <TextField label="Cân nặng (gross weight)" required type="number" min={0} step={0.1} suffix="kg" {...bind('shipment.grossWeight', { onChange: onShipmentInput })} />
          </FormGrid>
          <p className={styles.hint}>
            {t('Chứng từ không cần khai thêm — nội dung mặc định là Documents. Trên {kg}kg sẽ tự chuyển sang hàng hóa (PACK).', { kg: RULES.docMaxWeightKg })}
          </p>
        </>
      ) : (
        <>
          {/* Hàng hóa: mô tả tổng quan; số kiện và cân nặng khai theo từng dòng ở "Chi tiết kiện hàng" */}
          <FormGrid>
            <TextField
              label="Mô tả tổng quan hàng hóa (content)"
              required
              wide
              maxLength={RULES.contentMax}
              placeholder="Ghi bằng tiếng Anh (vd: Clothes, Dried food, Cosmetics)"
              {...bind('goods.description')}
            />
          </FormGrid>
        </>
      )}
      {docConverted && type === 'PACK' && (
        <p className={styles.notice} role="status">
          {t('Tài liệu trên {kg}kg được xem là hàng hóa. Hệ thống đã chuyển sang PACK — vui lòng khai Invoice đầy đủ.', { kg: RULES.docMaxWeightKg })}
        </p>
      )}
    </Card>
  );
}
