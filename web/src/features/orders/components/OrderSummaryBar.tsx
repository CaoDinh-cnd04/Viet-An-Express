import { useRef, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, DropdownMenu, Icon } from '@/shared/ui';
import { PAGE_SIZES, PRINT_DOCUMENTS } from '../constants';
import type { PrintDoc } from '../mutations';
import styles from './OrderSummaryBar.module.css';

interface OrderSummaryBarProps {
  selectedCount: number;
  pageSize: number;
  onPageSizeChange: (size: number) => void;
  /** In các đơn đã chọn với 1 hoặc nhiều loại chứng từ (Bill A4 / Invoice / CVCK / nhãn A6) trong 1 lần. */
  onBulkPrint: (docs: readonly PrintDoc[]) => void;
  onExport: () => void;
  /** Đang tạo file bảng kê. */
  exporting?: boolean;
}

/** Thao tác trên bảng đơn: số dòng / trang, in nhiều đơn (chọn loại chứng từ), xuất bảng kê (tổng kiện / cân nằm ở tổng quan đầu trang). */
export function OrderSummaryBar(p: OrderSummaryBarProps) {
  const { t } = useI18n();
  return (
    <div className={styles.bar}>
      <div className={styles.actions}>
        <label className={styles.pageSize}>
          {t('Hiển thị')}
          <select value={p.pageSize} onChange={e => p.onPageSizeChange(Number(e.target.value))}>
            {PAGE_SIZES.map(n => <option key={n} value={n}>{n}</option>)}
          </select>
        </label>
        <BulkPrintMenu selectedCount={p.selectedCount} onPrint={p.onBulkPrint} />
        <Button size="sm" onClick={p.onExport} disabled={p.exporting}>
          {t(p.exporting ? 'Đang xuất…' : 'Xuất bảng kê')}
        </Button>
      </div>
    </div>
  );
}

/** Nút "In nhiều bill": tick các loại chứng từ cần in rồi bấm In — nhiều loại in chung 1 lần. */
function BulkPrintMenu({ selectedCount, onPrint }: { selectedCount: number; onPrint: (docs: readonly PrintDoc[]) => void }) {
  const { t } = useI18n();
  const [docs, setDocs] = useState<readonly PrintDoc[]>(['bill-a4']);
  const close = useRef<() => void>(() => undefined);
  const toggleDoc = (d: PrintDoc) => setDocs(cur => (cur.includes(d) ? cur.filter(x => x !== d) : [...cur, d]));
  const print = () => {
    close.current();
    onPrint(PRINT_DOCUMENTS.map(d => d.key).filter(k => docs.includes(k)));
  };
  return (
    <DropdownMenu
      trigger={({ open, toggle }) => {
        close.current = () => open && toggle();
        return (
          <Button size="sm" onClick={toggle} disabled={!selectedCount} aria-expanded={open} aria-haspopup="dialog">
            {selectedCount ? t('In nhiều bill ({n})', { n: selectedCount }) : t('In nhiều bill')}
            <Icon name="chevronDown" size={14} />
          </Button>
        );
      }}
    >
      <div className={styles.printMenu}>
        <div className={styles.printTitle}>{t('Chọn chứng từ cần in')}</div>
        {PRINT_DOCUMENTS.map(d => (
          <label key={d.key} className={styles.printOption}>
            <input type="checkbox" checked={docs.includes(d.key)} onChange={() => toggleDoc(d.key)} />
            {t(d.label)}
          </label>
        ))}
        <p className={styles.printHint}>{t('Chọn nhiều loại: in lần lượt từng loại, mỗi loại 1 hộp thoại in theo đúng khổ giấy.')}</p>
        <Button variant="primary" size="sm" onClick={print} disabled={!docs.length}>
          <Icon name="printer" size={14} /> {t('In {n} đơn', { n: selectedCount })}
        </Button>
      </div>
    </DropdownMenu>
  );
}
