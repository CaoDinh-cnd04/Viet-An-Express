import { AdminSupport } from '../components/AdminSupport';

/**
 * Hỗ trợ &amp; góp ý khi nhân viên Việt An đăng nhập portal: trang quản lý như tab hỗ trợ của /admin
 * nhưng chỉ yêu cầu của khách mình phụ trách (IS_ADMIN / VIEW_CUSTOMER_ALL: mọi khách) — trả lời như CS Việt An, đổi trạng thái.
 */
export default function ManagedSupportPage() {
  return <AdminSupport source="managed" />;
}
