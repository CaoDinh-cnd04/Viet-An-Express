import { useRef, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, DropdownMenu, Icon } from '@/shared/ui';
import { PAGE_SIZES, PRINT_DOCUMENTS } from '../constants';
import type { PrintDoc } from '../mutations';
import type { ExportScope } from '../types';
import styles from './OrderSummaryBar.module.css';

/** In: đơn đã tick hoặc cả trang đang xem. */
export type PrintScope = 'selected' | 'page';

interface OrderSummaryBarProps {
  selectedCount: number;
  /** Số đơn trên trang đang xem (= số dòng / trang, trang cuối có thể ít hơn). */
  pageCount: number;
  /** Tổng số đơn khớp bộ lọc. */
  total: number;
  /** In đơn đã chọn / cả trang với 1 hoặc nhiều loại chứng từ (Bill A4 / Invoice / CVCK / nhãn A6) trong 1 lần. */
  onBulkPrint: (docs: readonly PrintDoc[], scope: PrintScope) => void;
  onExport: (scope: ExportScope) => void;
  /** Đang tạo file bảng kê. */
  exporting?: boolean;
}

/** Số dòng / trang — đặt ở chân bảng đơn. */
export function PageSizeSelect({ value, onChange }: { value: number; onChange: (size: number) => void }) {
  const { t } = useI18n();
  return (
    <label className={styles.pageSize}>
      {t('Hiển thị')}
      <select value={value} onChange={e => onChange(Number(e.target.value))}>
        {PAGE_SIZES.map(n => <option key={n} value={n}>{n}</option>)}
      </select>
    </label>
  );
}

/** Thao tác trên các đơn: in nhiều đơn (chọn loại chứng từ), xuất bảng kê — đặt cuối hàng bộ lọc. */
export function OrderSummaryBar(p: OrderSummaryBarProps) {
  return (
    <div className={styles.bar}>
      <div className={styles.actions}>
        <BulkPrintMenu selectedCount={p.selectedCount} pageCount={p.pageCount} onPrint={p.onBulkPrint} />
        <ExportMenu pageCount={p.pageCount} total={p.total} onExport={p.onExport} exporting={p.exporting} />
      </div>
    </div>
  );
}

/**
 * Nút "In nhiều bill": chọn in đơn đã tick hay cả trang đang xem (20 / 50 / 100 đơn theo số dòng / trang),
 * tick các loại chứng từ rồi bấm In — nhiều loại in chung 1 lần.
 */
function BulkPrintMenu({ selectedCount, pageCount, onPrint }: { selectedCount: number; pageCount: number; onPrint: (docs: readonly PrintDoc[], scope: PrintScope) => void }) {
  const { t } = useI18n();
  const [docs, setDocs] = useState<readonly PrintDoc[]>(['bill-a4']);
  const [pick, setPick] = useState<PrintScope | null>(null);
  const close = useRef<() => void>(() => undefined);
  // Chưa chọn phạm vi: có đơn tick thì in đơn đã tick, không thì in cả trang.
  const scope: PrintScope = selectedCount ? (pick ?? 'selected') : 'page';
  const count = scope === 'selected' ? selectedCount : pageCount;
  const toggleDoc = (d: PrintDoc) => setDocs(cur => (cur.includes(d) ? cur.filter(x => x !== d) : [...cur, d]));
  const print = () => {
    close.current();
    onPrint(PRINT_DOCUMENTS.map(d => d.key).filter(k => docs.includes(k)), scope);
  };
  return (
    <DropdownMenu
      trigger={({ open, toggle }) => {
        close.current = () => open && toggle();
        return (
          <Button size="sm" onClick={toggle} disabled={!pageCount && !selectedCount} aria-expanded={open} aria-haspopup="dialog">
            {selectedCount ? t('In nhiều bill ({n})', { n: selectedCount }) : t('In nhiều bill')}
            <Icon name="chevronDown" size={14} />
          </Button>
        );
      }}
    >
      <div className={styles.printMenu}>
        <div className={styles.printTitle}>{t('In đơn nào')}</div>
        <label className={styles.printOption}>
          <input type="radio" name="print-scope" checked={scope === 'selected'} disabled={!selectedCount} onChange={() => setPick('selected')} />
          {t('Đơn đã chọn ({n})', { n: selectedCount })}
        </label>
        <label className={styles.printOption}>
          <input type="radio" name="print-scope" checked={scope === 'page'} disabled={!pageCount} onChange={() => setPick('page')} />
          {t('Cả trang đang xem ({n} đơn)', { n: pageCount })}
        </label>
        <div className={styles.printTitle}>{t('Chọn chứng từ cần in')}</div>
        {PRINT_DOCUMENTS.map(d => (
          <label key={d.key} className={styles.printOption}>
            <input type="checkbox" checked={docs.includes(d.key)} onChange={() => toggleDoc(d.key)} />
            {t(d.label)}
          </label>
        ))}
        <p className={styles.printHint}>{t('Chọn nhiều loại: in lần lượt từng loại, mỗi loại 1 hộp thoại in theo đúng khổ giấy.')}</p>
        <Button variant="primary" size="sm" onClick={print} disabled={!docs.length || !count}>
          <Icon name="printer" size={14} /> {t('In {n} đơn', { n: count })}
        </Button>
      </div>
    </DropdownMenu>
  );
}

/** Nút "Xuất bảng kê": trang đang xem (đúng số dòng / trang) hoặc toàn bộ đơn khớp bộ lọc. */
function ExportMenu({ pageCount, total, onExport, exporting }: { pageCount: number; total: number; onExport: (scope: ExportScope) => void; exporting?: boolean }) {
  const { t } = useI18n();
  const close = useRef<() => void>(() => undefined);
  const run = (scope: ExportScope) => {
    close.current();
    onExport(scope);
  };
  return (
    <DropdownMenu
      trigger={({ open, toggle }) => {
        close.current = () => open && toggle();
        return (
          <Button size="sm" onClick={toggle} disabled={exporting || !total} aria-expanded={open} aria-haspopup="menu">
            {t(exporting ? 'Đang xuất…' : 'Xuất bảng kê')}
            <Icon name="chevronDown" size={14} />
          </Button>
        );
      }}
    >
      <div className={styles.exportMenu} role="menu">
        <button type="button" role="menuitem" onClick={() => run('page')} disabled={!pageCount}>
          {t('Trang đang xem ({n} đơn)', { n: pageCount })}
        </button>
        <button type="button" role="menuitem" onClick={() => run('all')}>
          {t('Toàn bộ dữ liệu ({n} đơn)', { n: total })}
        </button>
      </div>
    </DropdownMenu>
  );
}
