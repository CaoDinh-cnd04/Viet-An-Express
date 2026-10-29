/** Kiểu nghiệp vụ vận đơn dùng chung cho đơn hàng, tra cứu công khai và MyTracking — hợp đồng API: docs/API_CONTRACT.md §1. */

/** Nhóm lọc trạng thái (có thể trùng nhau): wait = chưa có ngày gửi · fly = có ngày gửi · nd = chưa có POD · ok = có POD. */
export type OrderStatus = 'wait' | 'fly' | 'nd' | 'ok';

/** Một mốc hành trình đơn. */
export interface OrderEvent {
  time: string;
  title: string;
  location?: string;
}
