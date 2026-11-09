import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getErrorMessage, http, type ListResponse } from '@/shared/api/http';
import { useLiveEvent } from '@/shared/api/live';
import { useToast } from '@/shared/ui';
import type { NewSupportRequest, SupportTicket } from './types';

/** Tên tín hiệu SignalR backend phát khi 1 yêu cầu đổi (Support/SupportLive.cs — SupportLiveHub.Event), tham số = mã yêu cầu. */
export const SUPPORT_LIVE_EVENT = 'support';

export const supportKeys = {
  all: ['troubles'] as const,
  detail: (id: string) => ['troubles', 'detail', id] as const
};

/** `enabled` false: không tải (vd trang đơn khách phụ trách của nhân viên Việt An — không có yêu cầu hỗ trợ của khách đang chọn). */
export function useSupportTickets(enabled = true) {
  return useQuery({
    enabled,
    queryKey: supportKeys.all,
    queryFn: () => http.get<ListResponse<SupportTicket>>('/troubles').then(r => r.data),
    // Dự phòng khi không có tín hiệu thời gian thực (CS sửa ở phần mềm cũ / mất kết nối).
    refetchInterval: 60_000
  });
}

/** Thời gian thực: CS trả lời / đổi trạng thái → máy chủ báo qua SignalR, tải lại danh sách yêu cầu (không cần tải lại trang). */
export function useSupportLive() {
  const qc = useQueryClient();
  useLiveEvent(SUPPORT_LIVE_EVENT, () => void qc.invalidateQueries({ queryKey: supportKeys.all }));
}

/** Gửi yêu cầu mới (hỗ trợ & sự cố / góp ý) — multipart/form-data để kèm tệp. */
export function useCreateSupportRequest() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: ({ file, ...fields }: NewSupportRequest) => {
      const body = new FormData();
      Object.entries(fields).forEach(([k, v]) => {
        if (v !== undefined && v !== null && v !== '') body.append(k, String(v));
      });
      if (file) body.append('file', file);
      return http.post<{ message: string; data: SupportTicket }>('/troubles', body);
    },
    onSuccess: res => {
      toast.show(res.message, 'success');
      void qc.invalidateQueries({ queryKey: supportKeys.all });
    },
    onError: e => toast.show(getErrorMessage(e), 'error')
  });
}

/** Khách trả lời yêu cầu đang "Chờ khách phản hồi" — backend chuyển lại "Đang xử lý" và báo email cho CS. */
export function useAnswerSupportTicket() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: ({ id, text }: { id: string; text: string }) =>
      http.post<{ message: string; data: SupportTicket }>(`/troubles/${encodeURIComponent(id)}/reply`, { text: text.trim() }),
    onSuccess: res => {
      toast.show(res.message, 'success');
      void qc.invalidateQueries({ queryKey: supportKeys.all });
    },
    onError: e => toast.show(getErrorMessage(e), 'error')
  });
}

/** Vận đơn gắn vào yêu cầu — chỉ các cột hộp thoại hiển thị (GET /orders). */
export interface BillSummary {
  bill: string;
  cnee: string;
  ct: string;
  pcs: string;
}

type BillPage = { items: BillSummary[] };

/** 5 đơn gần nhất của khách — gợi ý chọn nhanh mã vận đơn khi gửi yêu cầu. */
export function useRecentBills() {
  return useQuery({
    queryKey: [...supportKeys.all, 'recent-bills'],
    queryFn: () => http.get<BillPage>('/orders', { page: 1, pageSize: 5, sortBy: 'seq', sortDir: 'desc' }).then(r => r.items)
  });
}

/** Tra đúng 1 mã vận đơn của khách (trống → không gọi) — hiện người nhận / nước / số kiện để khách chắc đúng đơn. */
export function useBillLookup(bill: string) {
  return useQuery({
    queryKey: [...supportKeys.all, 'bill', bill],
    queryFn: () =>
      http.get<BillPage>('/orders', { q: `bill:${bill}`, searchField: 'bill', page: 1, pageSize: 1 })
        .then(r => r.items.find(o => o.bill === bill) ?? null),
    enabled: !!bill
  });
}
