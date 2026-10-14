import { Navigate, useSearchParams } from 'react-router-dom';
import { PERMISSIONS, useCan } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Card, PageHeader, Tabs } from '@/shared/ui';
import { EcomAddOrder } from '../components/EcomAddOrder';
import { EcomConnect } from '../components/EcomConnect';
import { EcomOrderList } from '../components/EcomOrderList';
import { ECOM_ORDERS_PATH, resolveEcomTab, type EcomTab } from '../lib/store-connection';
import styles from '../components/ecommerce.module.css';

const TABS: ReadonlyArray<{ key: EcomTab; label: string; permission?: string }> = [
  { key: 'orders', label: 'Đơn hàng' },
  { key: 'add', label: 'Thêm đơn', permission: PERMISSIONS.ecommerceOrders },
  { key: 'connect', label: 'Kết nối', permission: PERMISSIONS.ecommerceConnect }
];

/** E-commerce: đơn mới về (chờ xác nhận gửi), thêm đơn, kết nối sàn. Đơn đã xác nhận gửi ở trang riêng "Đơn hàng E-com". */
export default function EcommercePage() {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const allowed = useCan();
  // Tab cũ "Đơn hàng của tôi" đã thành trang riêng trên menu.
  if (params.get('tab') === 'mine') return <Navigate to={ECOM_ORDERS_PATH} replace />;
  // Tab không có quyền (tài khoản con) → ẩn, mở thẳng link thì về tab Đơn hàng.
  const tabs = TABS.filter(x => allowed(x.permission));
  const requested = resolveEcomTab(params.get('tab'));
  const tab = tabs.some(x => x.key === requested) ? requested : 'orders';

  return (
    <>
      <PageHeader title="E-commerce" description="Thêm đơn E-commerce bằng tay hoặc từ file CSV, xác nhận gửi cho Việt An." />
      <div className={cx('page-stack', styles.compactPage)}>
        <Card flush>
          <Tabs ariaLabel={t('Chức năng E-commerce')} items={tabs} value={tab} onChange={k => setParams({ tab: k }, { replace: true })} />
        </Card>
        {tab === 'orders' && <EcomOrderList scope="inbox" />}
        {tab === 'add' && <EcomAddOrder />}
        {tab === 'connect' && <EcomConnect />}
      </div>
    </>
  );
}
