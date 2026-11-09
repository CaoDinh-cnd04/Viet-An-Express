import { useState, type ClipboardEvent } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, SelectField, useToast } from '@/shared/ui';
import type { InvoiceItemValues } from '@/features/order-form';
import { INVOICE_COLUMNS, parseInvoicePaste } from '../lib/invoice-import';
import styles from './form.module.css';

interface InvoicePasteProps {
  /** Thêm các dòng đọc được vào invoice; `pkg` = kiện nhận hàng (tuyến khai invoice riêng từng kiện). */
  onItems: (items: InvoiceItemValues[], pkg: number) => void;
  /** Số kiện khi khai invoice riêng từng kiện; không có = invoice chung. */
  packageCount?: number;
  onClose: () => void;
}

/**
 * Dán dữ liệu Excel (như ô "Paste Excel Data" của hệ thống cũ): bôi chọn các ô trong Excel theo thứ tự cột file mẫu,
 * Ctrl+C rồi Ctrl+V vào ô này → thêm ngay thành dòng invoice (copy kèm dòng tiêu đề thì đọc theo tên cột).
 * Gõ / sửa tay rồi bấm "Thêm vào invoice" cũng được.
 */
export function InvoicePaste({ onItems, packageCount, onClose }: InvoicePasteProps) {
  const { t } = useI18n();
  const toast = useToast();
  const [text, setText] = useState('');
  const [pkg, setPkg] = useState('1');

  const add = (raw: string) => {
    const { items, skipped, incomplete = 0 } = parseInvoicePaste(raw);
    if (items.length) onItems(items, Number(pkg) || 1);
    const added = t('Đã thêm {n} mặt hàng vào Invoice', { n: items.length })
      + (incomplete ? t(' — {n} dòng còn trống SL / đơn giá, nhập tiếp trong bảng', { n: incomplete }) : '');
    const errors = skipped.length
      ? t(', bỏ qua {n} dòng lỗi ({list})', { n: skipped.length, list: skipped.slice(0, 2).map(s => t(s)).join('; ') + (skipped.length > 2 ? '…' : '') })
      : '';
    toast.show(items.length || skipped.length ? added + errors : t('Chưa có dữ liệu — copy các ô trong Excel rồi dán vào đây'), items.length ? 'success' : 'error');
    // Còn dòng lỗi thì giữ lại nội dung để khách sửa; thêm hết thì xóa ô.
    setText(skipped.length ? raw : '');
  };

  const onPaste = (e: ClipboardEvent<HTMLTextAreaElement>) => {
    const data = e.clipboardData.getData('text');
    if (!data.includes('\t') && !text) return; // không phải vùng ô Excel → để khách gõ / dán bình thường
    e.preventDefault();
    add(text ? `${text}\n${data}` : data);
  };

  return (
    <div className={styles.pastePanel}>
      <div className={styles.pasteHead}>
        <b>{t('Dán dữ liệu Excel')}</b>
        <span className={styles.pasteHint}>
          {t('Bôi chọn các ô trong Excel theo thứ tự cột: {cols} — Ctrl+C rồi Ctrl+V vào ô dưới. Copy kèm dòng tiêu đề cũng được.', {
            cols: INVOICE_COLUMNS.map(c => c.header).join(' · ')
          })}
        </span>
      </div>
      <textarea
        className={styles.pasteArea}
        rows={4}
        autoFocus
        value={text}
        placeholder={t('Dán dữ liệu Excel vào đây…')}
        aria-label={t('Dán dữ liệu Excel')}
        onChange={e => setText(e.target.value)}
        onPaste={onPaste}
      />
      <div className={styles.pasteActions}>
        {packageCount && packageCount > 1 && (
          <SelectField
            className={styles.pastePkg}
            label="Vào kiện"
            options={Array.from({ length: packageCount }, (_, i) => String(i + 1))}
            value={pkg}
            onChange={e => setPkg(e.target.value)}
          />
        )}
        <Button size="sm" variant="primary" disabled={!text.trim()} onClick={() => add(text)}>{t('Thêm vào invoice')}</Button>
        <Button size="sm" variant="ghost" onClick={onClose}>{t('Đóng')}</Button>
      </div>
    </div>
  );
}
