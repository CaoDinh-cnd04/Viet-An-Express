import { OrdersSourceContext } from '../source';
import OrdersPage from './OrdersPage';

/**
 * Nhân viên Việt An: đơn của mọi khách mình phụ trách (TCustomer.Staff_ID; vai trò Administrator = mọi khách) —
 * giao diện như "Đơn hàng của tôi", mỗi đơn ghi mã / tên khách. Chỉ xem, in, xuất bảng kê;
 * tạo / copy đơn (book bill) vẫn chọn khách rồi dùng trang của khách như cũ.
 */
export default function ManagedOrdersPage() {
  return (
    <OrdersSourceContext.Provider value="managed">
      <OrdersPage />
    </OrdersSourceContext.Provider>
  );
}
