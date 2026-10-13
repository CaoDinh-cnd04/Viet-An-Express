import type { OrderEvent, OrderStatus } from '@/features/orders/types';
import { parseBills } from './tracking';

/** Đường dẫn trang chi tiết tra cứu: /tracking/MA1,MA2 — dùng được để chia sẻ. */
export function trackingPath(bills: readonly string[], active?: string): string {
  const base = `/tracking/${bills.map(encodeURIComponent).join(',')}`;
  return active && active !== bills[0] ? `${base}?awb=${encodeURIComponent(active)}` : base;
}

/** Đọc danh sách mã từ tham số đường dẫn (chấp nhận cả dấu phẩy lẫn khoảng trắng). */
export function billsFromPath(param: string | undefined): string[] {
  return param ? parseBills(decodeURIComponent(param)).bills : [];
}

export interface EventLine {
  /** "17:39"; trống khi nguồn chỉ có ngày. */
  time: string;
  title: string;
  location?: string;
}

export interface EventDay {
  /** "28/09/2026"; "Không rõ ngày" khi không đọc được. */
  day: string;
  items: EventLine[];
}

const VN_DATE_TIME = /^(\d{2}\/\d{2}\/\d{4})(?:\s+(\d{2}:\d{2}))?/;
const ISO_DATE_TIME = /^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}:\d{2}))?/;
export const UNKNOWN_DAY = 'Không rõ ngày';

/** Tách "dd/MM/yyyy HH:mm", "dd/MM/yyyy" hoặc ISO "yyyy-MM-ddTHH:mm" thành ngày + giờ hiển thị. */
export function splitEventTime(value: string): { day: string; time: string } {
  const vn = VN_DATE_TIME.exec(value.trim());
  if (vn) return { day: vn[1]!, time: vn[2] ?? '' };
  const iso = ISO_DATE_TIME.exec(value.trim());
  if (iso) return { day: `${iso[3]}/${iso[2]}/${iso[1]}`, time: iso[4] ?? '' };
  return { day: UNKNOWN_DAY, time: '' };
}

/** Nhóm hành trình theo ngày, giữ thứ tự backend trả (mới nhất trước). */
export function groupEventsByDay(events: readonly OrderEvent[]): EventDay[] {
  const days: EventDay[] = [];
  for (const e of events) {
    const { day, time } = splitEventTime(e.time);
    const last = days[days.length - 1];
    const line: EventLine = { time, title: e.title, location: e.location };
    if (last && last.day === day) last.items.push(line);
    else days.push({ day, items: [line] });
  }
  return days;
}

/** Các bước hiển thị trên thanh tiến trình. */
export const TRACK_STEPS = ['Đã tạo vận đơn', 'Rời kho Việt An', 'Đang vận chuyển', 'Đã giao hàng'] as const;

export interface StepState {
  /** Bước hiện tại (0-based). */
  current: number;
  /** Bước hiện tại gặp sự cố (giao chưa thành công, quá hạn). */
  problem: boolean;
}

export function trackStep(status: OrderStatus): StepState {
  switch (status) {
    case 'wait':
      return { current: 0, problem: false };
    case 'fly':
      return { current: 2, problem: false };
    case 'nd':
      return { current: 2, problem: true };
    case 'ok':
      return { current: 3, problem: false };
  }
}
