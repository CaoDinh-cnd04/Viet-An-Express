import { useCallback, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useCellSelection } from '@/shared/lib/useCellSelection';
import { Button, Icon, StatusPill, useToast } from '@/shared/ui';
import { useOrder } from '../api';
import { useOrdersSource } from '../source';
import { ORDER_STATUS } from '../constants';
import { vaTrackingLink } from '@/shared/config/domain';
import { datePart } from '../lib/order-utils';
import type { Order, OrderActions, OrderSortField, SortDir } from '../types';
import { openTicketOf, SUPPORT_STATUS, useSupportTickets } from '@/features/support';
import { LabelButton, PrintButtons, RowTools } from './OrderRowActions';
import { TrackingLinks } from './TrackingLinks';
import styles from './OrdersTable.module.css';

interface OrdersTableProps {
  orders: Order[];
  /** Số thứ tự bắt đầu của trang hiện tại. */
  offset: number;
  sortBy: OrderSortField;
  sortDir: SortDir;
  onSort: (field: OrderSortField) => void;
  selected: ReadonlySet<string>;
  onToggle: (bill: string) => void;
  onToggleAll: (checked: boolean) => void;
  actions: OrderActions;
  loading?: boolean;
}

/** Giá trị chép ra khi bôi chọn ô — theo đúng thứ tự cột dữ liệu (các cột nút in không chọn được). */
type CopyValue = (o: Order, t: (s: string) => string) => string;
const COPY_VALUES: ReadonlyArray<CopyValue> = [
  o => o.ref ?? '',
  o => o.bill,
  o => o.connect ?? '',
  o => [o.cnee, o.ct].filter(Boolean).join(' / '),
  (o, t) => [o.sent, t(o.route)].filter(Boolean).join(' / '),
  o => [ORDER_STATUS[o.st].label, o.pod ? `${o.pod.date} ${o.pod.time}${o.pod.signer ? `, ${o.pod.signer}` : ''}` : ''].filter(Boolean).join(' · '),
  o => vaTrackingLink(o.bill),
  (o, t) => `${datePart(o.created)} (${t(o.pcs)}) ${o.content}`.trim()
];
/** Tên người gửi trên vận đơn; đơn cũ để trống thì lấy tên khách. */
const senderOf = (o: Order) => o.customer?.senderName || o.customer?.name || '';

/** Trang Đơn khách phụ trách: cột "Người gửi" (khách của đơn) đứng đầu, cột "Người tạo" sau cột Nhãn. */
const MANAGED_COPY_VALUES: ReadonlyArray<CopyValue> = [
  o => senderOf(o),
  ...COPY_VALUES,
  o => o.customer?.createdBy ?? ''
];

/**
 * Bảng "Đơn hàng của tôi" theo bố cục hệ thống cũ (chữ / khoảng cách gọn để vừa màn hình laptop):
 * Ref No · VA Bill · Mã hãng · Người nhận ↓ (kèm nước đến) · Ngày gửi (kèm dịch vụ) · Trạng thái / POD · Tracking · Ngày tạo · In · Nhãn A6 · thao tác.
 * Cột "POD dự kiến" và "Nước đến" tạm ẩn.
 * Mũi tên ↓ ở ô Người nhận xổ người liên hệ, SĐT và địa chỉ đầy đủ (mỗi dòng xổ riêng).
 */
type Column = { label: string; sort?: OrderSortField; className?: string };
const COLUMNS: ReadonlyArray<Column> = [
  { label: 'Ref No.', sort: 'ref', className: styles.colRef },
  { label: 'VA Bill', sort: 'bill', className: styles.colBill },
  { label: 'Mã hãng', className: styles.colCarrier },
  { label: 'Người nhận', sort: 'cnee', className: styles.colCnee },
  { label: 'Ngày gửi', sort: 'sent', className: styles.colDate },
  { label: 'Trạng thái / POD', sort: 'pod', className: styles.colPod },
  { label: 'Tracking', className: styles.colTracking },
  { label: 'Ngày tạo', sort: 'created', className: styles.colCreated },
  { label: 'In chứng từ', className: styles.colPrint },
  { label: 'Nhãn', className: styles.colLabel },
  { label: '', className: styles.colMore }
];

/**
 * Trang Đơn khách phụ trách (chỉ nhân viên Việt An): cột "Người gửi" (tên người gửi trên vận đơn, 1 dòng) ngoài cùng trước Ref No.,
 * cột "Người tạo" (tên đăng nhập tài khoản tạo đơn) sau cột Nhãn.
 */
const SENDER_COLUMN: Column = { label: 'Người gửi', className: styles.colSender };
const CREATOR_COLUMN: Column = { label: 'Người tạo', className: styles.colCreator };
const MANAGED_COLUMNS: ReadonlyArray<Column> = [SENDER_COLUMN, ...COLUMNS.slice(0, -1), CREATOR_COLUMN, COLUMNS.at(-1)!];

export function OrdersTable(props: OrdersTableProps) {
  const { orders, offset, sortBy, sortDir, onSort, selected, onToggle, onToggleAll, actions, loading } = props;
  const allChecked = orders.length > 0 && orders.every(o => selected.has(o.bill));
  const { t } = useI18n();
  const toast = useToast();
  const managed = useOrdersSource() === 'managed';
  const copyValues = managed ? MANAGED_COPY_VALUES : COPY_VALUES;
  const value = useCallback((row: number, col: number) => (orders[row] ? (copyValues[col]?.(orders[row], t) ?? '') : ''), [orders, t, copyValues]);
  const cells = useCellSelection(value, n => toast.show(t('Đã sao chép {n} ô — dán được vào Excel', { n }), 'success'));
  const columns = managed ? MANAGED_COLUMNS : COLUMNS;

  return (
    <div className={styles.scroll}>
      {cells.count > 0 && (
        <div className={styles.cellBar} data-cell-toolbar>
          <span>{t('Đã chọn {n} ô', { n: cells.count })}</span>
          <Button size="sm" variant="primary" onClick={() => void cells.copy()}>
            {t('Sao chép')} <kbd className={styles.kbd}>Ctrl+C</kbd>
          </Button>
          <Button size="sm" variant="ghost" onClick={cells.clear}>{t('Bỏ chọn')}</Button>
        </div>
      )}
      <table ref={cells.tableRef} className={cx(styles.table, loading && styles.loading, cells.selecting && styles.selecting)}>
        <thead>
          <tr>
            <th className={styles.colCheck}>
              <input type="checkbox" aria-label={t('Chọn tất cả đơn trên trang')} checked={allChecked} onChange={e => onToggleAll(e.target.checked)} />
            </th>
            {columns.map((col, ci) => (
              <th key={ci} className={col.className} aria-sort={col.sort && col.sort === sortBy ? (sortDir === 'asc' ? 'ascending' : 'descending') : undefined}>
                {col.sort ? (
                  <button type="button" className={styles.sortBtn} onClick={() => onSort(col.sort!)}>
                    {t(col.label)}
                    <span className={styles.sortMark}>{col.sort === sortBy ? (sortDir === 'asc' ? '▲' : '▼') : ''}</span>
                  </button>
                ) : (
                  col.label && t(col.label)
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {orders.map((o, i) => (
            <OrderRow
              key={o.bill}
              order={o}
              index={offset + i + 1}
              checked={selected.has(o.bill)}
              onToggle={onToggle}
              actions={actions}
              cell={col => cells.cellProps(i, col)}
            />
          ))}
          {!orders.length && (
            <tr>
              <td colSpan={columns.length + 1} className={styles.empty}>{t(loading ? 'Đang tải…' : 'Không có đơn nào khớp bộ lọc.')}</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

interface OrderRowProps {
  order: Order;
  index: number;
  checked: boolean;
  onToggle: (bill: string) => void;
  actions: OrderActions;
  /** Thuộc tính chọn ô cho cột dữ liệu thứ `col` (0 = cột dữ liệu đầu tiên: Ref No., hoặc Người gửi ở trang phụ trách). */
  cell: (col: number) => ReturnType<ReturnType<typeof useCellSelection>['cellProps']>;
}

/** Mũi tên ↓ xổ / thu gọn chi tiết trong ô (không mở ngăn chi tiết đơn). */
function ExpandButton({ open, label, onToggle }: { open: boolean; label: string; onToggle: () => void }) {
  return (
    <button type="button" className={styles.expandBtn} aria-expanded={open} aria-label={label} title={label} onClick={onToggle}>
      <Icon name="chevronDown" size={14} className={cx(styles.chev, open && styles.chevOpen)} />
    </button>
  );
}

function Nil({ children = '—' }: { children?: string }) {
  const { t } = useI18n();
  return <span className={styles.nil}>{t(children)}</span>;
}

function OrderRow({ order: o, index, checked, onToggle, actions, cell: cellAt }: OrderRowProps) {
  const { t } = useI18n();
  const status = ORDER_STATUS[o.st];
  const [showDetail, setShowDetail] = useState(false);
  // Danh sách thường có sẵn người nhận; backend cũ chưa trả thì tải chi tiết đơn khi khách bấm xổ
  const needDetail = showDetail && !o.receiver;
  const detail = useOrder(needDetail ? o.bill : null);
  const r = o.receiver ?? detail.data?.receiver ?? null;
  const loadingDetail = needDetail && detail.isLoading;
  const addressLines = r ? [r.addr1, r.addr2, r.addr3].filter(Boolean) : [];
  // Sự cố chưa xong của đơn (danh sách sự cố tải 1 lần, dùng chung mọi dòng).
  const managed = useOrdersSource() === 'managed';
  const tickets = useSupportTickets(!managed);
  const ticket = openTicketOf(tickets.data ?? [], o.bill);
  // Trang phụ trách có thêm cột Người gửi đứng đầu → các cột dữ liệu sau lùi 1.
  const cell = (col: number) => cellAt(managed ? col + 1 : col);

  return (
    // Bấm dòng không mở đơn nữa (dễ mở nhầm khi bôi chọn ô) — xem chi tiết bằng nút con mắt cuối dòng.
    <tr className={cx(styles.row, checked && styles.rowChecked)}>
      <td className={styles.colCheck}>
        <div className={styles.no}>{index}</div>
        <input type="checkbox" aria-label={t('Chọn đơn {bill}', { bill: o.bill })} checked={checked} onChange={() => onToggle(o.bill)} />
      </td>
      {managed && (
        <td className={styles.colSender} {...cellAt(0)}>
          {senderOf(o) ? <span className={styles.sender} title={o.customer ? `${o.customer.code} — ${o.customer.name}` : undefined}>{senderOf(o)}</span> : <Nil />}
        </td>
      )}
      <td className={styles.colRef} {...cell(0)}>{o.ref ? <span className="tabular">{o.ref}</span> : null}</td>
      <td className={styles.colBill} {...cell(1)}>
        <div className={styles.bill}>{o.bill}</div>
      </td>
      <td className={styles.colCarrier} {...cell(2)} title={o.connect ? t('Mã tracking hãng / last-mile') : undefined}>
        {o.connect && <span className={styles.connect}>{o.connect}</span>}
      </td>
      <td className={styles.colCnee} {...cell(3)}>
        <div className={styles.expandLine}>
          <span className={styles.cnee}>{o.cnee}</span>
          <ExpandButton open={showDetail} label={t('Xem liên hệ, địa chỉ người nhận')} onToggle={() => setShowDetail(v => !v)} />
        </div>
        {o.ct && <div className={styles.cneeCountry}>{o.ct}</div>}
        {o.createdByVietAn && <StatusPill tone="brand">{t('Việt An tạo giúp')}</StatusPill>}
        {showDetail && loadingDetail && <div className={styles.more}><Nil>{'Đang tải…'}</Nil></div>}
        {showDetail && r && (
          <div className={styles.more}>
            <div>ATT: {r.contact || '—'}</div>
            <div className="tabular">Tel: {r.tel || '—'}</div>
            {r.email && <div>Email: {r.email}</div>}
            {addressLines.map((line, k) => <div key={k}>{line}</div>)}
            <div>Postcode: {r.postal}</div>
            <div>City: {r.city}</div>
            <div>State: {r.state}</div>
            {r.taxId && <div>Tax ID: {r.taxId}</div>}
          </div>
        )}
      </td>
      <td className={styles.colDate} {...cell(4)}>
        {/* Dịch vụ - hub trên (xuống dòng trong ô hẹp), ngày gửi dưới */}
        {o.route && <div className={styles.route}>{t(o.route)}</div>}
        {o.sent ? <div className="tabular">{o.sent}</div> : <Nil>{'Chưa gửi'}</Nil>}
      </td>
      <td className={styles.colPod} {...cell(5)}>
        <StatusPill tone={status.tone}>{status.label}</StatusPill>
        {ticket && (
          <div>
            <span className={styles.ticketTag} title={t('Sự cố {id}: {type} — bấm biểu tượng con mắt để xem', { id: ticket.id, type: t(ticket.type) })}>
              <Icon name="alert" size={12} /> {t('Sự cố')} · {t(SUPPORT_STATUS[ticket.status].label)}
            </span>
          </div>
        )}
        {o.pod ? (
          <>
            <div className={cx('tabular', styles.podDate)}>{o.pod.date}</div>
            <div className="tabular">
              {o.pod.time}
              {o.pod.signer ? `, ${o.pod.signer}` : ''}
            </div>
          </>
        ) : null}
      </td>
      <td className={styles.colTracking} {...cell(6)} data-tour="orders-tracking"><TrackingLinks bill={o.bill} refNo={o.ref} /></td>
      <td className={styles.colCreated} {...cell(7)}>
        {/* 3 dòng: ngày tạo · kiện, cân · tên hàng */}
        <div className="tabular">{datePart(o.created)}</div>
        <div className={styles.pcs}>{t(o.pcs)}</div>
        {o.content && <div className={styles.content}>{o.content}</div>}
      </td>
      {/* Mốc hướng dẫn trùng ở mọi dòng — hướng dẫn chỉ vào dòng đầu tiên */}
      <td className={styles.colPrint} data-tour="orders-print"><PrintButtons order={o} /></td>
      <td className={styles.colLabel} data-tour="orders-label"><LabelButton order={o} /></td>
      {managed && (
        <td className={styles.colCreator} {...cell(8)}>
          {o.customer?.createdBy ? <span className={styles.creator}>{o.customer.createdBy}</span> : <Nil />}
        </td>
      )}
      <td className={styles.colMore} data-tour="orders-tools"><RowTools order={o} actions={actions} /></td>
    </tr>
  );
}
