import { lazy, Suspense, type ComponentType, type LazyExoticComponent } from 'react';
import { createBrowserRouter, Navigate, useLocation, useParams, type RouteObject } from 'react-router-dom';
import { CHOOSE_CUSTOMER_PATH, isVietAnAgent, MANAGED_ORDERS_PATH, PERMISSIONS, RequireAuth, RequirePermission, useCan, useSession } from '@/features/auth';
import LandingPage from '@/features/landing/pages/LandingPage';
import TrackingPage from '@/features/landing/pages/TrackingPage';
import { isMyTrackingHost } from '@/features/mytracking';
import { AppShell } from './layout/AppShell';
import { NotFoundPage } from './pages/NotFoundPage';

/** Dữ liệu gắn với route — Topbar đọc `title` để hiện breadcrumb. */
export interface RouteHandle {
  title: string;
}

/** Chuyển sang đường dẫn chuẩn, giữ nguyên query (vd ?draft=…) — cho đường dẫn cũ / gõ tay quen thuộc. */
function RedirectKeepQuery({ to }: { to: string }) {
  const { search, hash } = useLocation();
  return <Navigate to={`${to}${search}${hash}`} replace />;
}

// Mỗi trang tách chunk riêng, chỉ tải khi mở.
const CreateOrderPage = lazy(() => import('@/features/create-order/pages/CreateOrderPage'));
const PublicMyTrackingPage = lazy(() => import('@/features/mytracking/pages/PublicMyTrackingPage'));

/** /tracking/{x}: trên tên miền MyTracking (mytracking.top) x là mã khách → trang của khách; tên miền khác → tra cứu công khai. */
function TrackingEntry() {
  const { bills } = useParams();
  return bills && isMyTrackingHost(window.location.hostname)
    ? <Suspense fallback={null}><PublicMyTrackingPage customerCode={bills} /></Suspense>
    : <TrackingPage />;
}
const AdminPage = lazy(() => import('@/features/admin/pages/AdminPage'));
const PrintPage = lazy(() => import('@/features/orders/pages/PrintPage'));
const pages = {
  myTracking: lazy(() => import('@/features/mytracking/pages/MyTrackingPage')),
  staff: lazy(() => import('@/features/staff/pages/StaffPage')),
  orderImport: lazy(() => import('@/features/order-import/pages/OrderImportPage')),
  orders: lazy(() => import('@/features/orders/pages/OrdersPage')),
  managedOrders: lazy(() => import('@/features/orders/pages/ManagedOrdersPage')),
  drafts: lazy(() => import('@/features/drafts/pages/DraftsPage')),
  pricing: lazy(() => import('@/features/pricing/pages/PricingPage')),
  ecommerce: lazy(() => import('@/features/ecommerce/pages/EcommercePage')),
  ecomOrders: lazy(() => import('@/features/ecommerce/pages/EcomOrdersPage')),
  support: lazy(() => import('@/features/support/pages/SupportPage')),
  managedSupport: lazy(() => import('@/features/admin/pages/ManagedSupportPage')),
  chooseCustomer: lazy(() => import('@/features/auth/pages/ChooseCustomerPage')),
  notifications: lazy(() => import('@/features/notifications/pages/NotificationsPage')),
  apiTracking: lazy(() => import('@/features/account/pages/ApiTrackingPage')),
  password: lazy(() => import('@/features/account/pages/ChangePasswordPage'))
} satisfies Record<string, LazyExoticComponent<ComponentType>>;

/**
 * Khai báo 1 trang: đường dẫn + tiêu đề breadcrumb (+ quyền cần có, giống nav.config.ts).
 * Thêm trang mới: thêm 1 dòng ở đây và 1 dòng ở nav.config.ts.
 */
const page = (path: string, title: string, Page: ComponentType, permission?: string): RouteObject => ({
  path,
  element: permission ? <RequirePermission permission={permission}><Page /></RequirePermission> : <Page />,
  handle: { title } satisfies RouteHandle
});

/** Hỗ trợ & góp ý: nhân viên Việt An thấy trang quản lý yêu cầu của khách mình phụ trách, khách thấy trang gửi yêu cầu. */
function SupportEntry() {
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  // Nhân viên đã chọn khách → mặc định chỉ yêu cầu của khách đó; chưa chọn → mọi khách trong phạm vi.
  return isVietAnAgent(user) ? <pages.managedSupport customerCode={user?.customerCode || undefined} companyName={user?.companyName} /> : <pages.support />;
}

function HomeRedirect() {
  const allowed = useCan();
  return <Navigate to={allowed(PERMISSIONS.shipmentsCreate) ? '/orders/new' : '/orders'} replace />;
}

const create = (mode: 'wizard' | 'quick') => (
  <RequirePermission permission={PERMISSIONS.shipmentsCreate}><CreateOrderPage mode={mode} /></RequirePermission>
);

const routes: RouteObject[] = [
  // Trang ngoài (không cần đăng nhập) — tải ngay, không lazy.
  { path: '/', element: <LandingPage /> },
  { path: '/login', element: <LandingPage /> },
  // Kết quả tra cứu công khai: /tracking/MA1,MA2 (?awb= chọn mã đang xem).
  { path: '/tracking/:bills?', element: <TrackingEntry /> },
  // Trang MyTracking của khách: /tracking/{mã khách}/{số bill}?ref={mã REF} (link gửi người nhận hàng, mọi tên miền).
  { path: '/tracking/:code/:bill', element: <Suspense fallback={null}><PublicMyTrackingPage /></Suspense> },
  // Trang quản trị Việt An: đăng nhập tài khoản quản trị riêng (không phải tài khoản khách).
  { path: '/admin', element: <Suspense fallback={null}><AdminPage /></Suspense> },
  // Tab in chứng từ (mở từ nút In, noopener): không có khung portal; API tự báo lỗi nếu hết phiên.
  { path: '/print/:job', element: <Suspense fallback={null}><PrintPage /></Suspense> },
  // Portal: phải đăng nhập, chưa có phiên thì chuyển về /login?next=...
  {
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      // Không có trang chủ riêng: sau đăng nhập vào Tạo đơn (không có quyền tạo đơn thì Đơn hàng của tôi).
      { path: 'home', element: <HomeRedirect /> },
      { path: 'orders/new', element: create('wizard'), handle: { title: 'Tạo đơn hàng' } satisfies RouteHandle },
      { path: 'orders/new/quick', element: create('quick'), handle: { title: 'Tạo đơn 1 trang' } satisfies RouteHandle },
      // Đường dẫn quen gõ tay — chuyển về form tạo đơn thay vì báo 404.
      { path: 'orders/create', element: <RedirectKeepQuery to="/orders/new" /> },
      { path: 'orders/create/quick', element: <RedirectKeepQuery to="/orders/new/quick" /> },
      page('orders/import', 'Tạo đơn từ Excel', pages.orderImport, PERMISSIONS.shipmentsCreate),
      page('orders', 'Đơn hàng của tôi', pages.orders, PERMISSIONS.shipmentsView),
      page(MANAGED_ORDERS_PATH.slice(1), 'Đơn khách phụ trách', pages.managedOrders, PERMISSIONS.managedOrders),
      page('drafts', 'Đơn nháp & chưa in', pages.drafts, PERMISSIONS.shipmentsView),
      page('pricing', 'Giá & gợi ý dịch vụ', pages.pricing),
      page('ecommerce', 'E-commerce', pages.ecommerce, PERMISSIONS.ecommerceView),
      page('ecommerce/orders', 'Đơn hàng E-com', pages.ecomOrders, PERMISSIONS.ecommerceView),
      page('troubles', 'Hỗ trợ & góp ý', SupportEntry),
      page(CHOOSE_CUSTOMER_PATH.slice(1), 'Chọn khách', pages.chooseCustomer, PERMISSIONS.actForCustomer),
      page('notifications', 'Thông báo', pages.notifications),
      // Trang Trợ giúp & Góp ý cũ đã gộp vào Hỗ trợ & sự cố.
      { path: 'help', element: <RedirectKeepQuery to="/troubles" /> },
      { path: 'account/feedback', element: <RedirectKeepQuery to="/troubles" /> },
      page('account/api-tracking', 'API Tracking', pages.apiTracking),
      page('account/mytracking', 'MyTracking cá nhân', pages.myTracking, PERMISSIONS.myTracking),
      page('account/staff', 'Tài khoản nhân viên', pages.staff, PERMISSIONS.manageStaff),
      page('account/password', 'Đổi mật khẩu', pages.password),
      // Sửa thông tin người gửi nay gửi bằng yêu cầu hỗ trợ.
      { path: 'account/sender-info', element: <Navigate to="/troubles?new=sender-info" replace /> },
      { path: '*', element: <NotFoundPage />, handle: { title: 'Không tìm thấy trang' } satisfies RouteHandle }
    ]
  }
];

export const router = createBrowserRouter(routes);
