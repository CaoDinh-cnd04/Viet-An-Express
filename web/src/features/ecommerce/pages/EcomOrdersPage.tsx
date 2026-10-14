import { PageHeader } from '@/shared/ui';
import { EcomOrderList } from '../components/EcomOrderList';

/**
 * Trang "Đơn hàng E-com" (menu Dịch vụ & Bán hàng): đơn e-com khách đã xác nhận gửi —
 * lưu ở dbo.DonTMDT, không liên quan dbo.MaVanDon / trang "Đơn hàng của tôi" của vận đơn.
 */
export default function EcomOrdersPage() {
  return (
    <>
      <PageHeader title="Đơn hàng E-com" description="Đơn từ file CSV hoặc nhập tay đã xác nhận gửi — in nhãn, phiếu đóng gói, bảng kê giao cho Việt An." />
      <EcomOrderList scope="mine" />
    </>
  );
}
