import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getErrorMessage, http } from '@/shared/api/http';
import { useToast } from '@/shared/ui';
import { orderKeys } from '@/features/orders/api';

/** Đơn nháp — docs/API_CONTRACT.md §2. 'draft' = đang làm dở, 'ready' = đã xong, chờ in. */
export interface Draft {
  id: string;
  stt: 'draft' | 'ready';
  cnee: string;
  ct: string;
  service: string;
  branch: string;
  ref: string;
  pcs: string;
  content: string;
  date: string;
  payload?: Record<string, unknown>;
}

export type NewDraft = Omit<Draft, 'id' | 'date'>;

export const draftKeys = { all: ['drafts'] as const };

export function useDrafts() {
  return useQuery({
    queryKey: draftKeys.all,
    queryFn: () => http.get<{ data: Draft[] }>('/drafts').then(r => r.data)
  });
}

export function useSaveDraft() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (draft: NewDraft) => http.post<{ data: Draft }>('/drafts', draft).then(r => r.data),
    onSuccess: () => void qc.invalidateQueries({ queryKey: draftKeys.all })
  });
}

/** Cập nhật đơn nháp đang sửa (mở từ nút Sửa). Endpoint mới — xem API_CONTRACT.md. */
export function useUpdateDraft() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, draft }: { id: string; draft: NewDraft }) => http.put<{ data: Draft }>(`/drafts/${encodeURIComponent(id)}`, draft).then(r => r.data),
    // Lỗi cũng tải lại: nháp có thể vừa được in / xóa ở nơi khác (DRAFT_CHANGED).
    onSettled: () => void qc.invalidateQueries({ queryKey: draftKeys.all })
  });
}

export function useDeleteDraft() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (id: string) => http.delete(`/drafts/${encodeURIComponent(id)}`),
    onSuccess: () => toast.show('Đã xóa đơn nháp', 'success'),
    onError: e => toast.show(getErrorMessage(e), 'error'),
    // Lỗi cũng tải lại: nháp có thể vừa được in ở nơi khác (DRAFT_CHANGED).
    onSettled: () => void qc.invalidateQueries({ queryKey: draftKeys.all })
  });
}

/** In đơn: backend cấp mã bill, khóa đơn và chuyển sang "Đơn hàng của tôi". */
export function usePrintDraft() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (id: string) => http.post<{ message: string; billCode: string }>(`/drafts/${encodeURIComponent(id)}/print`),
    onSuccess: res => toast.show(res.message, 'success'),
    onError: e => toast.show(getErrorMessage(e), 'error'),
    // Lỗi cũng tải lại: người khác vừa in / xóa đơn này thì danh sách cập nhật theo.
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: draftKeys.all });
      void qc.invalidateQueries({ queryKey: orderKeys.all });
    }
  });
}
