import { useCallback, useMemo, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, Pagination } from '@/shared/ui';
import { useOrders } from '../api';
import { NewSupportRequestDialog } from '@/features/support';
import { OrderDrawer } from '../components/OrderDrawer';
import { OrderPhotosDialog } from '../components/OrderPhotosDialog';
import { OrderFilterBar } from '../components/OrderFilterBar';
import { OrderOverview } from '../components/OrderOverview';
import { OrderSummaryBar, PageSizeSelect } from '../components/OrderSummaryBar';
import { OrdersTable } from '../components/OrdersTable';
import { useOrderFilters } from '../hooks/useOrderFilters';
import { useExportOrders, usePrintDocuments } from '../mutations';
import type { Order, OrderActions, OrderSortField } from '../types';
import styles from './OrdersPage.module.css';

export default function OrdersPage() {
  const { t } = useI18n();
  const print = usePrintDocuments();
  const exportOrders = useExportOrders();
  const { filters, update, reset } = useOrderFilters();
  const { data, isFetching, isError, refetch } = useOrders(filters);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [openOrder, setOpenOrder] = useState<Order | null>(null);
  const [photoOrder, setPhotoOrder] = useState<Order | null>(null);
  const [issueOrder, setIssueOrder] = useState<Order | null>(null);
  const actions = useMemo<OrderActions>(() => ({ onOpen: setOpenOrder, onPhotos: setPhotoOrder, onReportIssue: setIssueOrder }), []);

  const items = data?.items ?? [];
  const summary = data?.summary;

  const onSort = (field: OrderSortField) =>
    update({ sortBy: field, sortDir: filters.sortBy === field && filters.sortDir === 'asc' ? 'desc' : 'asc' });

  const toggle = (bill: string) =>
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(bill)) next.delete(bill);
      else next.add(bill);
      return next;
    });

  const toggleAll = (checked: boolean) =>
    setSelected(prev => {
      const next = new Set(prev);
      items.forEach(o => (checked ? next.add(o.bill) : next.delete(o.bill)));
      return next;
    });

  const closeDrawer = useCallback(() => setOpenOrder(null), []);

  return (
    <>

      {/* 1 cụm gọn: hàng 1 trạng thái + tổng, hàng 2 bộ lọc + Xóa lọc + In nhiều bill + Xuất bảng kê */}
      <div className={styles.panel}>
        <OrderOverview
          status={filters.status}
          onStatusChange={status => update({ status })}
          counts={summary?.statusCounts}
          totalPieces={summary?.totalPieces ?? 0}
          totalWeight={summary?.totalWeight ?? 0}
          totalRows={data?.total ?? 0}
        />

        <OrderFilterBar
          filters={filters}
          onChange={update}
          onReset={reset}
          actions={
            <OrderSummaryBar
              selectedCount={selected.size}
              pageCount={items.length}
              total={data?.total ?? 0}
              onBulkPrint={(docs, scope) => void print(scope === 'page' ? items.map(o => o.bill) : [...selected], docs)}
              onExport={scope => exportOrders.mutate({ filters, scope })}
              exporting={exportOrders.isPending}
            />
          }
        />
      </div>

      <Card flush>
        {isError ? (
          <EmptyState
            title="Không tải được danh sách đơn"
            description="Kiểm tra kết nối tới máy chủ rồi thử lại."
            action={<Button onClick={() => void refetch()}>{t('Thử lại')}</Button>}
          />
        ) : (
          <OrdersTable
            orders={items}
            offset={((data?.page ?? 1) - 1) * filters.pageSize}
            sortBy={filters.sortBy}
            sortDir={filters.sortDir}
            onSort={onSort}
            selected={selected}
            onToggle={toggle}
            onToggleAll={toggleAll}
            actions={actions}
            loading={isFetching}
          />
        )}

        <footer className={styles.footer}>
          <span className={styles.footerLeft}>
            {t('{n} đơn hàng', { n: data?.total ?? 0 })}
            <PageSizeSelect value={filters.pageSize} onChange={pageSize => update({ pageSize })} />
          </span>
          {data && (
            <Pagination page={data.page} totalPages={data.totalPages} total={data.total} pageSize={data.pageSize} onChange={page => update({ page })} />
          )}
        </footer>
      </Card>

      <OrderDrawer order={openOrder} onClose={closeDrawer} actions={actions} />
      <OrderPhotosDialog order={photoOrder} onClose={() => setPhotoOrder(null)} />
      {issueOrder && (
        <NewSupportRequestDialog open preset={{ kind: 'ho_tro', bill: issueOrder.bill }} onClose={() => setIssueOrder(null)} />
      )}
    </>
  );
}
