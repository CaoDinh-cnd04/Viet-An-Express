import { ManagedSupport } from '../components/ManagedSupport';

/**
 * Hỗ trợ & góp ý khi nhân viên Việt An đăng nhập portal: cùng bố cục trang của khách, chỉ yêu cầu của khách mình phụ trách
 * (vai trò Administrator: mọi khách) — trả lời như CS Việt An, đổi trạng thái. Đã chọn khách → mặc định chỉ khách đó.
 */
export default function ManagedSupportPage({ customerCode, companyName }: { customerCode?: string; companyName?: string }) {
  return <ManagedSupport actingCustomer={customerCode ? { code: customerCode, name: companyName ?? '' } : undefined} />;
}
