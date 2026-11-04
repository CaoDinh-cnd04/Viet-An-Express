import type { ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { ORDER_STATUS } from '../constants';
import { ALL_STATUSES, parseStatuses, toggleStatus } from '../lib/multi-filter';
import type { OrderStatus } from '@/shared/model/shipment';
import type { OrderListResponse } from '../types';
import styles from './OrderOverview.module.css';

interface OrderOverviewProps {
  /** "all" hoặc danh sách "wait,fly". */
  status: string;
  onStatusChange: (value: string) => void;
  counts?: OrderListResponse['summary']['statusCounts'];
  totalPieces: number;
  totalWeight: number;
  totalRows: number;
  /** Thao tác đặt cuối hàng (Hiển thị / In nhiều bill / Xuất bảng kê). */
  actions?: ReactNode;
}

/**
 * Đầu trang Đơn hàng: 5 ô số liệu bấm được (Tất cả + 4 trạng thái) — nền trắng, chấm màu + nhãn, số đơn, "% tổng" và
 * thanh tỉ lệ theo màu trạng thái; ô đang chọn đặc màu chữ trắng. Bấm để lọc, chọn được nhiều; "Tất cả" bỏ hết lựa chọn.
 * Dòng dưới (căn phải, không khung): tổng kiện / tổng cân / số kết quả theo bộ lọc đang áp + thao tác.
 */
export function OrderOverview({ status, onStatusChange, counts, totalPieces, totalWeight, totalRows, actions }: OrderOverviewProps) {
  const { t } = useI18n();
  const fmt = new Intl.NumberFormat('vi-VN');
  const picked = new Set(parseStatuses(status));
  const all = picked.size === 0;
  const percent = (n: number) => (counts?.all ? Math.round((n / counts.all) * 100) : 0);

  const tile = (key: 'all' | OrderStatus, label: string, on: boolean, onClick: () => void) => {
    const n = counts?.[key];
    const p = n === undefined ? 0 : percent(n);
    return (
      <button key={key} type="button" aria-pressed={on} className={cx(styles.tile, styles[key], on && styles.on)} onClick={onClick}>
        <span className={styles.label}>{t(label)}</span>
        <span className={styles.figure}>
          <span className={styles.count}>{n === undefined ? '—' : fmt.format(n)}</span>
          {n !== undefined && <span className={styles.share}>{t('{p} tổng', { p: `${p}%` })}</span>}
        </span>
        <span className={styles.bar} aria-hidden><span style={{ width: `${p}%` }} /></span>
      </button>
    );
  };

  return (
    <section className={styles.overview} aria-label={t('Tổng quan đơn hàng')} data-tour="orders-status">
      <div className={styles.tiles} role="group" aria-label={t('Lọc theo trạng thái (chọn được nhiều)')}>
        {tile('all', 'Tất cả', all, () => onStatusChange('all'))}
        {ALL_STATUSES.map(s => tile(s, ORDER_STATUS[s].label, picked.has(s), () => onStatusChange(toggleStatus(status, s))))}
      </div>
      <div className={styles.bottom}>
        <dl className={styles.totals}>
          <div><dt>{t('Tổng kiện')}</dt><dd>{fmt.format(totalPieces)}</dd></div>
          <div><dt>{t('Tổng cân')}</dt><dd>{fmt.format(totalWeight)} kg</dd></div>
          <div><dt>{t('Kết quả')}</dt><dd>{fmt.format(totalRows)} {t('đơn')}</dd></div>
        </dl>
        {actions && <div className={styles.actions}>{actions}</div>}
      </div>
    </section>
  );
}
