import { useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { Icon } from '@/shared/ui';
import type { CreateOrderValues, InvoiceItemValues } from '../schema';
import { HsLookupDialog } from './HsLookupDialog';
import { InvoiceToolbar } from './InvoiceToolbar';
import styles from './QuickLookup.module.css';

/**
 * Khung "Hỗ trợ khách hàng" cạnh form tạo đơn:
 * - Tra mã HS: mở popup tra mã theo nước nhập khẩu (US / UK / AU).
 * - Khai invoice: thư viện mặt hàng, chép invoice cũ, nhập từ Excel (kèm tải file mẫu) — thêm thẳng vào bảng Invoice.
 */
export function QuickLookup() {
  const { t } = useI18n();
  const { getValues, setValue } = useFormContext<CreateOrderValues>();
  const isDoc = useWatch<CreateOrderValues, 'shipment.type'>({ name: 'shipment.type' }) === 'DOC';
  const [hsOpen, setHsOpen] = useState(false);

  /** Thêm / thay dòng hàng của bảng Invoice (bảng Invoice dùng useFieldArray cùng tên nên tự cập nhật). */
  const writeItems = (list: InvoiceItemValues[], mode: 'append' | 'replace') => {
    const items = getValues('invoice.items') ?? [];
    // Thay dòng trống mặc định thay vì để lại dòng rỗng phía trên
    const onlyBlank = items.length === 1 && !items[0]?.descEn && !items[0]?.price;
    setValue('invoice.items', mode === 'replace' || onlyBlank ? list : [...items, ...list], { shouldDirty: true });
  };

  return (
    <section className={styles.box} aria-label={t('Hỗ trợ khách hàng')}>
      <div className={styles.title}><Icon name="help" size={15} /> {t('Hỗ trợ khách hàng')}</div>

      <button type="button" className={styles.hsButton} onClick={() => setHsOpen(true)}>
        <Icon name="search" size={16} />
        <span><b>{t('Tra mã HS')}</b><small>{t('Theo nước nhập khẩu: Mỹ, Anh, Úc')}</small></span>
        <Icon name="chevronRight" size={14} />
      </button>

      <div className={styles.section}>{t('Khai invoice')}</div>
      {isDoc ? <p className={styles.hint}>{t('Đơn chứng từ (DOC) không cần khai invoice.')}</p> : <InvoiceToolbar onItems={writeItems} />}

      <HsLookupDialog open={hsOpen} onClose={() => setHsOpen(false)} />
    </section>
  );
}
