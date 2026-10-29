import { useQuery } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import type { CreateOrderValues } from '@/features/order-form';

// Tạo đơn mới từ đơn đã gửi.

/** Toàn bộ form của 1 đơn đã gửi để tạo đơn mới từ đơn đó (GET /orders/:bill/copy) — chỉ đọc. */
export function useOrderCopy(bill: string | null) {
  return useQuery({
    queryKey: ['order-copy', bill ?? ''],
    queryFn: () => http.get<{ data: Partial<CreateOrderValues> }>(`/orders/${encodeURIComponent(bill ?? '')}/copy`).then(r => r.data),
    enabled: !!bill,
    retry: false
  });
}
