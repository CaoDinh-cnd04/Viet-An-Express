import { Link } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { formatNumber } from '@/shared/lib/format';
import { Card, DataTable, EmptyState, Icon, LinkButton, PageHeader, StatCard, StatGrid, StatusPill, type Column, type IconName } from '@/shared/ui';
import { useDrafts } from '@/features/drafts/api';
import { useNotifications } from '@/features/notifications';
import { useOrders } from '@/features/orders/api';
import { DEFAULT_FILTERS, ORDER_STATUS } from '@/features/orders/constants';
import type { Order } from '@/features/orders/types';
import { kindOf, useTroubles } from '@/features/troubles';
import styles from './DashboardPage.module.css';

const RECENT = { ...DEFAULT_FILTERS, pageSize: 6 };

const QUICK_ACTIONS: ReadonlyArray<{ to: string; label: string; icon: IconName }> = [
  { to: '/orders/new', label: 'Tạo đơn mới', icon: 'filePlus' },
  { to: '/orders/import', label: 'Tạo đơn từ Excel', icon: 'upload' },
  { to: '/pricing', label: 'Tra cứu giá', icon: 'tag' },
  { to: '/troubles', label: 'Báo sự cố', icon: 'alert' }
];

const columns = (t: (text: string) => string): ReadonlyArray<Column<Order>> => [
  { key: 'bill', header: 'VA Bill', render: o => <span className="mono">{o.bill}</span> },
  { key: 'cnee', header: 'Người nhận', render: o => o.cnee },
  { key: 'ct', header: 'Nước đến', render: o => o.ct },
  { key: 'pcs', header: 'Kiện / cân', render: o => t(o.pcs) },
  { key: 'st', header: 'Trạng thái', render: o => <StatusPill tone={ORDER_STATUS[o.st].tone}>{ORDER_STATUS[o.st].label}</StatusPill> }
];

export default function DashboardPage() {
  const { t } = useI18n();
  const orders = useOrders(RECENT);
  const drafts = useDrafts();
  const troubles = useTroubles();
  const notifications = useNotifications();

  const counts = orders.data?.summary.statusCounts;
  const openTroubles = (troubles.data ?? []).filter(x => kindOf(x) === 'su_co' && x.status !== 'done').length;
  const readyDrafts = (drafts.data ?? []).filter(d => d.stt === 'ready').length;
  const latestNotices = (notifications.data?.data ?? []).slice(0, 4);

  return (
    <>
      <PageHeader title="Trang chủ" description="Tổng quan đơn hàng và việc cần làm hôm nay." />
      <div className="page-stack">
        <StatGrid>
          <StatCard label="Tổng đơn" value={formatNumber(counts?.all ?? 0)} hint={t('{kg} kg · {n} kiện', { kg: formatNumber(orders.data?.summary.totalWeight ?? 0), n: orders.data?.summary.totalPieces ?? 0 })} tone="brand" icon="box" />
          <StatCard label="Chờ in bill" value={readyDrafts} hint={t('{n} đơn nháp & chưa in', { n: drafts.data?.length ?? 0 })} icon="printer" />
          <StatCard label="Chưa phát" value={counts?.nd ?? 0} hint={t('Đơn chưa có thông tin POD')} icon="clock" />
          <StatCard label="Sự cố chưa xong" value={openTroubles} tone={openTroubles ? 'warning' : undefined} icon="alert" />
        </StatGrid>

        <div className={styles.quick}>
          {QUICK_ACTIONS.map(a => (
            <Link key={a.to} to={a.to} className={styles.quickItem}>
              <Icon name={a.icon} size={20} />
              <span>{t(a.label)}</span>
            </Link>
          ))}
        </div>

        <div className={styles.grid}>
          <Card flush title="Đơn mới nhất" actions={<LinkButton to="/orders" size="sm" variant="ghost">{t('Xem tất cả')}</LinkButton>}>
            <DataTable caption="Đơn mới nhất" columns={columns(t)} rows={orders.data?.items ?? []} rowKey={o => o.bill} loading={orders.isLoading} minWidth={560} empty={{ title: 'Chưa có đơn hàng' }} />
          </Card>

          <div className="page-stack">
            <Card title="Thông báo" actions={<LinkButton to="/notifications" size="sm" variant="ghost">{t('Tất cả')}</LinkButton>}>
              {latestNotices.length ? (
                <ul className={styles.list}>
                  {latestNotices.map(n => (
                    <li key={n.id}>
                      <Link to="/notifications" className={styles.listItem}>
                        <strong>{n.title}</strong>
                        <span>{n.date}{n.imp ? ` · ${t('Quan trọng')}` : ''}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="Không có thông báo mới" />
              )}
            </Card>
          </div>
        </div>
      </div>
    </>
  );
}
