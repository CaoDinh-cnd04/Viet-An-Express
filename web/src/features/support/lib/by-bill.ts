import type { SupportTicket } from '../types';

/** So khớp mã bill không phân biệt hoa thường / khoảng trắng. */
const key = (bill: string) => bill.trim().toUpperCase();

/** Sự cố của 1 vận đơn, mới cập nhật trước (giữ thứ tự danh sách từ API). Hàm thuần — có test. */
export function ticketsOfBill(tickets: readonly SupportTicket[], bill: string): SupportTicket[] {
  const k = key(bill);
  return tickets.filter(t => key(t.bill) === k);
}

/** Sự cố chưa xong (khác "Đã xử lý") cần nhắc trên dòng đơn — sự cố đầu tiên chưa xong, không có thì null. */
export function openTicketOf(tickets: readonly SupportTicket[], bill: string): SupportTicket | null {
  return ticketsOfBill(tickets, bill).find(t => t.status !== 'done') ?? null;
}
