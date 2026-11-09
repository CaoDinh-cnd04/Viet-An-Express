import { createContext, useContext } from 'react';

/**
 * Nguồn đơn của trang danh sách: `mine` = "Đơn hàng của tôi" (đơn của khách đang đăng nhập / đang chọn);
 * `managed` = nhân viên Việt An xem đơn mọi khách mình phụ trách (GET /vietan/orders…).
 * Cùng giao diện; trang `managed` chỉ xem / in / xuất — tạo, copy đơn vẫn chọn khách như cũ.
 */
export type OrdersSource = 'mine' | 'managed';

export const OrdersSourceContext = createContext<OrdersSource>('mine');

export const useOrdersSource = () => useContext(OrdersSourceContext);

/** Gốc API theo nguồn. Hàm thuần — có test. */
export const ordersApiBase = (source: OrdersSource) => (source === 'managed' ? '/vietan/orders' : '/orders');
