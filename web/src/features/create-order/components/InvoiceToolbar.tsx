import { useRef, useState } from 'react';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { readFileAsText } from '@/shared/lib/files';
import { Icon, Modal, Notice, useToast } from '@/shared/ui';
import { importInvoiceFile, INVOICE_TEMPLATE_URL, useProductLibrary, useRecentInvoices } from '../api';
import { parseInvoiceCsv } from '../lib/invoice-import';
import type { InvoiceItemValues } from '@/features/order-form';
import { invoiceItemFromProduct } from '../lib/product-library';
import { ProductLibraryList } from './ProductLibraryList';
import styles from './form.module.css';
import panel from './QuickLookup.module.css';

interface InvoiceToolbarProps {
  /** Thêm dòng vào invoice; replace = thay toàn bộ (dùng khi chép invoice cũ). */
  onItems: (items: InvoiceItemValues[], mode: 'append' | 'replace') => void;
}

/**
 * Khai invoice nhanh (tab "Khai invoice" của khung Hỗ trợ khách hàng): thư viện mặt hàng, chép invoice cũ,
 * nhập từ Excel (file mẫu Mau_Import_Invoice.xlsx, đọc ở backend; vẫn nhận .csv mẫu cũ) kèm tải file mẫu Excel.
 */
export function InvoiceToolbar({ onItems }: InvoiceToolbarProps) {
  const toast = useToast();
  const { t } = useI18n();
  const fileRef = useRef<HTMLInputElement>(null);
  const [dialog, setDialog] = useState<'library' | 'recent' | null>(null);
  const library = useProductLibrary(dialog === 'library');
  const recent = useRecentInvoices(dialog === 'recent');

  const [importing, setImporting] = useState(false);
  // Excel (.xlsx) theo file mẫu: đọc ở backend; .csv (mẫu cũ) đọc ngay trên trình duyệt
  const importFile = async (file: File | undefined) => {
    if (!file) return;
    if (!/\.(xlsx|csv)$/i.test(file.name)) return toast.show('Chỉ nhận file Excel (.xlsx) theo mẫu hoặc .csv', 'error');
    let items: InvoiceItemValues[];
    let skipped: string[];
    if (/\.csv$/i.test(file.name)) ({ items, skipped } = parseInvoiceCsv(await readFileAsText(file)));
    else {
      setImporting(true);
      try {
        ({ items, skipped } = await importInvoiceFile(file));
      } catch (e) {
        return toast.show(getErrorMessage(e, 'Không đọc được file Excel'), 'error');
      } finally {
        setImporting(false);
      }
    }
    if (items.length) onItems(items, 'append');
    const added = t('Đã thêm {n} mặt hàng vào Invoice', { n: items.length });
    const errors = skipped.length
      ? t(', bỏ qua {n} dòng lỗi ({list})', { n: skipped.length, list: skipped.slice(0, 2).map(s => t(s)).join('; ') + (skipped.length > 2 ? '…' : '') })
      : '';
    toast.show(added + errors, items.length ? 'success' : 'error');
  };

  const unavailable = (e: unknown) =>
    isNotImplemented(e) ? <Notice tone="warning">{t('Chức năng đang được kết nối máy chủ.')}</Notice> : <Notice tone="danger">{t('Không tải được dữ liệu.')}</Notice>;

  return (
    <div className={panel.actions}>
      <button type="button" className={panel.action} onClick={() => setDialog('library')}>
        <Icon name="bag" size={15} />
        <span><b>{t('Thư viện mặt hàng')}</b><small>{t('Chọn hàng đã lưu')}</small></span>
      </button>
      <button type="button" className={panel.action} onClick={() => setDialog('recent')}>
        <Icon name="clock" size={15} />
        <span><b>{t('Invoice cũ')}</b><small>{t('Chép từ đơn trước')}</small></span>
      </button>
      {/* Nhập từ Excel | File mẫu: 1 chức năng, nút tách đôi */}
      <div className={panel.split}>
        <button type="button" className={panel.action} onClick={() => fileRef.current?.click()} disabled={importing}>
          <Icon name="upload" size={15} />
          <span><b>{t(importing ? 'Đang đọc file…' : 'Nhập từ Excel')}</b><small>{t('File Excel theo mẫu, tối đa 49 mặt hàng')}</small></span>
        </button>
        <a className={panel.splitSide} href={INVOICE_TEMPLATE_URL} download="Mau_Import_Invoice.xlsx">
          <Icon name="download" size={13} /> {t('Tải file mẫu Excel')}
        </a>
      </div>
      <input ref={fileRef} type="file" accept=".xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv" className="visually-hidden" tabIndex={-1} onChange={e => { void importFile(e.target.files?.[0]); e.target.value = ''; }} />

      <Modal open={dialog === 'library'} title="Thư viện mặt hàng" size="lg" onClose={() => setDialog(null)}>
        {library.isError ? unavailable(library.error) : library.isLoading ? <p className={styles.pickerEmpty}>{t('Đang tải…')}</p> : !library.data?.length ? (
          <p className={styles.pickerEmpty}>{t('Chưa có mặt hàng đã lưu.')}</p>
        ) : (
          <ProductLibraryList products={library.data} onPick={p => { onItems([invoiceItemFromProduct(p)], 'append'); setDialog(null); toast.show(t('Đã thêm vào Invoice'), 'success'); }} />
        )}
      </Modal>

      <Modal open={dialog === 'recent'} title="Chép invoice từ đơn cũ" size="lg" onClose={() => setDialog(null)}>
        {recent.isError ? unavailable(recent.error) : recent.isLoading ? <p className={styles.pickerEmpty}>{t('Đang tải…')}</p> : !recent.data?.length ? (
          <p className={styles.pickerEmpty}>{t('Chưa có invoice nào.')}</p>
        ) : (
          <ul className={styles.pickerList}>
            {recent.data.map(inv => (
              <li key={inv.bill}>
                <button type="button" className={styles.pickerItem} onClick={() => { onItems(inv.items.map(invoiceItemFromProduct), 'replace'); setDialog(null); toast.show(t('Đã chép invoice {bill}', { bill: inv.bill }), 'success'); }}>
                  <span className={styles.pickerText}>
                    <strong>{inv.bill} · {inv.cnee}</strong>
                    <span>{inv.date} · {t('{n} mặt hàng', { n: inv.items.length })} · {inv.currency}</span>
                  </span>
                  <span className={styles.pickerGo}>{t('Chép')}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </div>
  );
}
