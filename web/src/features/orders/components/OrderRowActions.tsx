import { useNavigate } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import { usePrintDocuments, type PrintDoc } from '../mutations';
import type { Order, OrderActions } from '../types';
import styles from './OrderRowActions.module.css';

/** Nút in chứng từ đặt thẳng trên dòng như hệ thống cũ (không phải mở menu). */
/** Nhãn nút ngắn (không chữ "In"); câu đầy đủ hiện khi rê chuột. */
const PRINT_BUTTONS: ReadonlyArray<{ doc: PrintDoc; label: string; hint?: string }> = [
  { doc: 'bill-a4', label: 'Bill A4', hint: 'In Bill A4' },
  { doc: 'invoice', label: 'Invoice', hint: 'In Invoice' },
  { doc: 'cvck', label: 'CVCK' }
];

/** Cột in: Bill A4 · Invoice · CVCK xếp dọc. */
export function PrintButtons({ order }: { order: Order }) {
  const { t } = useI18n();
  const print = usePrintDocuments();
  return (
    <div className={styles.stack}>
      {PRINT_BUTTONS.map(b => (
        <button key={b.doc} type="button" className={styles.pill} title={b.hint && t(b.hint)} onClick={() => void print([order.bill], b.doc)}>
          {b.label}
        </button>
      ))}
    </div>
  );
}

/** Cột nhãn: In nhãn A6 — chỉ shipping mark, 1 nhãn / kiện để dán thùng. */
export function LabelButton({ order }: { order: Order }) {
  const { t } = useI18n();
  const print = usePrintDocuments();
  return (
    <button type="button" className={cx(styles.pill, styles.label, styles.tall)} title={t('In nhãn A6')} onClick={() => void print([order.bill], 'label-a6')}>
      Label A6
    </button>
  );
}

/**
 * Cột thao tác cuối dòng: 4 nút biểu tượng nhỏ xếp 1 cột dọc (không còn menu "…" trùng với nút xem):
 * xem chi tiết · ảnh kiện hàng · copy đơn · báo sự cố. Rê chuột hiện tên thao tác.
 */
export function RowTools({ order, actions }: { order: Order; actions: OrderActions }) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const tools = [
    { icon: 'eye', label: t('Xem chi tiết đơn {bill}', { bill: order.bill }), onClick: () => actions.onOpen(order) },
    { icon: 'image', label: order.photos ? t('Xem ảnh kiện hàng ({n})', { n: order.photos }) : t('Xem ảnh kiện hàng'), onClick: () => actions.onPhotos(order) },
    { icon: 'copy', label: t('Copy đơn'), onClick: () => navigate(`/orders/new?from=${order.bill}`) },
    { icon: 'alert', label: t('Báo sự cố'), onClick: () => actions.onTrouble(order) }
  ] as const;
  return (
    <div className={styles.tools}>
      {tools.map(x => (
        <button key={x.icon} type="button" className={styles.tool} title={x.label} aria-label={x.label} onClick={x.onClick}>
          <Icon name={x.icon} size={12} />
        </button>
      ))}
    </div>
  );
}
