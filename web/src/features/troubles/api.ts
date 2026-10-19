import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getErrorMessage, http, type ListResponse } from '@/shared/api/http';
import { useToast } from '@/shared/ui';
import type { NewTrouble, TroubleTicket } from './types';

export const troubleKeys = {
  all: ['troubles'] as const,
  detail: (id: string) => ['troubles', 'detail', id] as const
};

export function useTroubles() {
  return useQuery({
    queryKey: troubleKeys.all,
    queryFn: () => http.get<ListResponse<TroubleTicket>>('/troubles').then(r => r.data)
  });
}

/** Gửi yêu cầu mới (sự cố / hỗ trợ / góp ý) — multipart/form-data để kèm tệp. */
export function useCreateTrouble() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: ({ file, ...fields }: NewTrouble) => {
      const body = new FormData();
      Object.entries(fields).forEach(([k, v]) => {
        if (v !== undefined && v !== null && v !== '') body.append(k, String(v));
      });
      if (file) body.append('file', file);
      return http.post<{ message: string; data: TroubleTicket }>('/troubles', body);
    },
    onSuccess: res => {
      toast.show(res.message, 'success');
      void qc.invalidateQueries({ queryKey: troubleKeys.all });
    },
    onError: e => toast.show(getErrorMessage(e), 'error')
  });
}
