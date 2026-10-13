import { lazy, Suspense, type ComponentType, type LazyExoticComponent } from 'react';
import { createBrowserRouter, Navigate, useLocation, type RouteObject } from 'react-router-dom';
import { PERMISSIONS, RequireAuth, RequirePermission } from '@/features/auth';
import LandingPage from '@/features/landing/pages/LandingPage';
import TrackingPage from '@/features/landing/pages/TrackingPage';
import { AppShell } from './layout/AppShell';
import { NotFoundPage } from './pages/NotFoundPage';
import { UsageTrackingLayout } from './UsageTrackingLayout';

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
const PrivacyPage = lazy(() => import('@/features/landing/pages/PrivacyPage'));
const ShopifyConnectPage = lazy(() => import('@/features/ecommerce/pages/ShopifyConnectPage'));
const AdminPage = lazy(() => import('@/features/admin/pages/AdminPage'));
const pages = {
  myTracking: lazy(() => import('@/features/mytracking/pages/MyTrackingPage')),
  staff: lazy(() => import('@/features/staff/pages/StaffPage')),
  orderImport: lazy(() => import('@/features/order-import/pages/OrderImportPage')),
  orders: lazy(() => import('@/features/orders/pages/OrdersPage')),
  drafts: lazy(() => import('@/features/drafts/pages/DraftsPage')),
  pricing: lazy(() => import('@/features/pricing/pages/PricingPage')),
  ecommerce: lazy(() => import('@/features/ecommerce/pages/EcommercePage')),
  ecomOrders: lazy(() => import('@/features/ecommerce/pages/EcomOrdersPage')),
  troubles: lazy(() => import('@/features/troubles/pages/TroublesPage')),
  notifications: lazy(() => import('@/features/notifications/pages/NotificationsPage')),
  support: lazy(() => import('@/features/support/pages/SupportPage')),
  apiTracking: lazy(() => import('@/features/account/pages/ApiTrackingPage')),
  password: lazy(() => import('@/features/account/pages/ChangePasswordPage')),
  feedback: lazy(() => import('@/features/feedback/pages/FeedbackPage'))
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

const create = (mode: 'wizard' | 'quick') => (
  <RequirePermission permission={PERMISSIONS.shipmentsCreate}><CreateOrderPage mode={mode} /></RequirePermission>
);

const routes: RouteObject[] = [
  // Trang ngoài (không cần đăng nhập) — tải ngay, không lazy.
  { path: '/', element: <LandingPage /> },
  { path: '/login', element: <LandingPage /> },
  // Kết quả tra cứu công khai: /tracking/MA1,MA2 (?awb= chọn mã đang xem).
  { path: '/tracking/:bills?', element: <TrackingPage /> },
  // Trang MyTracking công khai của khách: /t/{đường dẫn} (?bills= mã đang tra).
  { path: '/t/:slug', element: <Suspense fallback={null}><PublicMyTrackingPage /></Suspense> },
  // Chính sách bảo mật app Shopify (link khai trong App Store listing).
  { path: '/privacy', element: <Suspense fallback={null}><PrivacyPage /></Suspense> },
  // Mở app từ Shopify (backend OAuth xong chuyển về đây): công khai, tự đăng nhập tại chỗ rồi gắn shop vào tài khoản.
  { path: '/shopify', element: <Suspense fallback={null}><ShopifyConnectPage /></Suspense> },
  // Trang quản trị Việt An: đăng nhập tài khoản quản trị riêng (không phải tài khoản khách).
  { path: '/admin', element: <Suspense fallback={null}><AdminPage /></Suspense> },
  // Portal: phải đăng nhập, chưa có phiên thì chuyển về /login?next=...
  {
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      // Bỏ trang chủ riêng: link cũ /home chuyển sang Đơn hàng của tôi
      { path: 'home', element: <Navigate to="/orders" replace /> },
      { path: 'orders/new', element: create('wizard'), handle: { title: 'Tạo đơn hàng' } satisfies RouteHandle },
      { path: 'orders/new/quick', element: create('quick'), handle: { title: 'Tạo đơn 1 trang' } satisfies RouteHandle },
      // Đường dẫn quen gõ tay — chuyển về form tạo đơn thay vì báo 404.
      { path: 'orders/create', element: <RedirectKeepQuery to="/orders/new" /> },
      { path: 'orders/create/quick', element: <RedirectKeepQuery to="/orders/new/quick" /> },
      page('orders/import', 'Tạo đơn từ Excel', pages.orderImport, PERMISSIONS.shipmentsCreate),
      page('orders', 'Đơn hàng của tôi', pages.orders, PERMISSIONS.shipmentsView),
      page('drafts', 'Đơn nháp & chưa in', pages.drafts, PERMISSIONS.shipmentsView),
      page('pricing', 'Giá & gợi ý dịch vụ', pages.pricing),
      page('ecommerce', 'E-commerce', pages.ecommerce, PERMISSIONS.ecommerceView),
      page('ecommerce/orders', 'Đơn hàng E-com', pages.ecomOrders, PERMISSIONS.ecommerceView),
      // Đường cũ của trang mở app từ Shopify — giữ để link cũ vẫn chạy.
      { path: 'ecommerce/shopify', element: <Navigate to="/shopify" replace /> },
      page('troubles', 'Quản lý sự cố', pages.troubles),
      page('notifications', 'Thông báo', pages.notifications),
      page('help', 'Trợ giúp & Góp ý', pages.support),
      page('account/api-tracking', 'API Tracking', pages.apiTracking),
      page('account/mytracking', 'MyTracking cá nhân', pages.myTracking, PERMISSIONS.myTracking),
      page('account/staff', 'Tài khoản nhân viên', pages.staff, PERMISSIONS.manageStaff),
      page('account/password', 'Đổi mật khẩu', pages.password),
      page('account/feedback', 'Góp ý', pages.feedback, PERMISSIONS.feedback),
      { path: '*', element: <NotFoundPage />, handle: { title: 'Không tìm thấy trang' } satisfies RouteHandle }
    ]
  }
];

export const router = createBrowserRouter([{ element: <UsageTrackingLayout />, children: routes }]);
