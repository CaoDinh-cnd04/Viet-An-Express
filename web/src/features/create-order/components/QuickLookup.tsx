import { useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { isPerPackageInvoice } from '@/shared/config/domain';
import { useI18n } from '@/shared/i18n';
import { Icon } from '@/shared/ui';
import { packageCount, writeInvoiceItems } from '../lib/package-invoice';
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
  const [hub, packages = []] = useWatch<CreateOrderValues, ['service.hub', 'packages']>({ name: ['service.hub', 'packages'] });
  // Tuyến khai invoice riêng từng kiện: chọn kiện nhận mặt hàng thêm từ thư viện / invoice cũ / Excel.
  const perPackage = !isDoc && isPerPackageInvoice(hub);
  const count = packageCount(packages);
  const [target, setTarget] = useState(1);
  const pkg = Math.min(Math.max(1, target), Math.max(1, count));

  /** Thêm / thay dòng hàng của bảng Invoice (bảng Invoice dùng useFieldArray cùng tên nên tự cập nhật). */
  const writeItems = (list: InvoiceItemValues[], mode: 'append' | 'replace') => {
    // Thay dòng trống sẵn có thay vì để lại dòng rỗng phía trên; invoice từng kiện: chỉ ghi vào kiện đang chọn.
    setValue('invoice.items', writeInvoiceItems(getValues('invoice.items') ?? [], list, mode, perPackage ? pkg : undefined), { shouldDirty: true });
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
      {perPackage && count > 1 && (
        <label className={styles.target}>
          <span>{t('Thêm vào')}</span>
          <select value={pkg} onChange={e => setTarget(Number(e.target.value))}>
            {Array.from({ length: count }, (_, i) => (
              <option key={i + 1} value={i + 1}>{t('Kiện {n}/{count}', { n: i + 1, count })}</option>
            ))}
          </select>
        </label>
      )}
      {isDoc ? <p className={styles.hint}>{t('Đơn chứng từ (DOC) không cần khai invoice.')}</p> : <InvoiceToolbar onItems={writeItems} />}

      <HsLookupDialog open={hsOpen} onClose={() => setHsOpen(false)} />
    </section>
  );
}
