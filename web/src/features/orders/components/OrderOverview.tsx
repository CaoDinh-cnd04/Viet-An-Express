import type { ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { ORDER_STATUS } from '../constants';
import { ALL_STATUSES, parseStatuses, toggleStatus } from '../lib/multi-filter';
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
 * Thanh tổng quan đầu trang Đơn hàng (1 hàng gọn): trạng thái dạng nút có chấm màu + số đếm (bấm để lọc, chọn được nhiều;
 * "Tất cả" bỏ hết lựa chọn) · bên phải tổng kiện / tổng cân / số kết quả theo bộ lọc đang áp.
 */
export function OrderOverview({ status, onStatusChange, counts, totalPieces, totalWeight, totalRows, actions }: OrderOverviewProps) {
  const { t } = useI18n();
  const fmt = new Intl.NumberFormat('vi-VN');
  const picked = new Set(parseStatuses(status));
  const all = picked.size === 0;

  return (
    <section className={styles.overview} aria-label={t('Tổng quan đơn hàng')}>
      <div className={styles.statuses} role="group" aria-label={t('Lọc theo trạng thái (chọn được nhiều)')}>
        <button type="button" aria-pressed={all} className={cx(styles.status, styles.all, all && styles.on)} onClick={() => onStatusChange('all')}>
          {t('Tất cả')}
          <span className={styles.count}>{counts ? fmt.format(counts.all) : '—'}</span>
        </button>
        {ALL_STATUSES.map(s => {
          const on = picked.has(s);
          return (
            <button
              key={s}
              type="button"
              aria-pressed={on}
              className={cx(styles.status, styles[ORDER_STATUS[s].tone], on && styles.on)}
              onClick={() => onStatusChange(toggleStatus(status, s))}
            >
              <span className={styles.dot} aria-hidden="true" />
              {t(ORDER_STATUS[s].label)}
              <span className={styles.count}>{counts ? fmt.format(counts[s]) : '—'}</span>
            </button>
          );
        })}
      </div>
      <dl className={styles.totals}>
        <div><dt>{t('Tổng kiện')}</dt><dd>{fmt.format(totalPieces)}</dd></div>
        <div><dt>{t('Tổng cân')}</dt><dd>{fmt.format(totalWeight)} kg</dd></div>
        <div><dt>{t('Kết quả')}</dt><dd>{fmt.format(totalRows)} {t('đơn')}</dd></div>
      </dl>
      {actions && <div className={styles.actions}>{actions}</div>}
    </section>
  );
}
