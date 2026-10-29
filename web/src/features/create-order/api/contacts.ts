import { useQuery } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import type { RecentReceiver } from '../lib/recent-receivers';
import type { RecentSender } from '../lib/recent-senders';

// Người gửi / người nhận: hồ sơ người gửi, người đã dùng ở đơn trước, sổ địa chỉ người nhận.

/** Hồ sơ người gửi (backend SenderProfile). */
export interface SenderProfile {
  id?: string;
  n: string;
  c: string;
  t: string;
  d: string;
}

export function useSenders() {
  return useQuery({
    queryKey: ['addresses', 'senders'],
    queryFn: () => http.get<{ data: SenderProfile[] }>('/addresses/senders').then(r => r.data)
  });
}

/** Người nhận đã gửi trước đây có tên công ty chứa `q` (đơn cũ của khách). API_CONTRACT.md §1. */
export function useRecentReceivers(query: string | null) {
  return useQuery({
    queryKey: ['orders', 'receivers', query],
    queryFn: () => http.get<{ data: RecentReceiver[] }>('/orders/receivers', { q: query }).then(r => r.data),
    enabled: !!query,
    staleTime: 60_000,
    placeholderData: previous => previous,
    retry: false
  });
}

/** Người gửi đã dùng ở đơn trước, tên công ty / người liên hệ chứa `query` (gợi ý cho tài khoản con khai người gửi). */
export function useRecentSenders(query: string | null) {
  return useQuery({
    queryKey: ['orders', 'senders', query],
    queryFn: () => http.get<{ data: RecentSender[] }>('/orders/senders', { q: query }).then(r => r.data),
    enabled: !!query,
    staleTime: 60_000,
    placeholderData: previous => previous,
    retry: false
  });
}

/** Sổ địa chỉ người nhận: 100 người nhận gần nhất từ đơn cũ của khách (GET /orders/receivers không có q). */
export function useReceiverBook(enabled: boolean) {
  return useQuery({
    queryKey: ['orders', 'receivers', 'book'],
    queryFn: () => http.get<{ data: RecentReceiver[] }>('/orders/receivers').then(r => r.data),
    enabled,
    staleTime: 60_000
  });
}
