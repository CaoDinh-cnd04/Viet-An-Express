import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http, ApiError } from '@/shared/api/http';
import type { SenderInfo, SenderOverview, SenderRequest } from './types';
export const senderKey = ['sender-info'] as const;
const retry = (count: number, e: unknown) => !(e instanceof ApiError && [401, 403].includes(e.status)) && count < 1;
export function useSenderInfo() {
  return useQuery({ queryKey: senderKey, queryFn: () => http.get<{ data: SenderOverview }>('/account/sender-info').then(r => r.data), refetchInterval: 15_000, retry });
}
export function useRequestSenderInfo() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (body: SenderInfo & { note: string | null }) => http.post<{ data: SenderRequest }>('/account/sender-info/requests', body).then(r => r.data),
    onSuccess: () => void qc.invalidateQueries({ queryKey: senderKey }), onError: () => void qc.invalidateQueries({ queryKey: senderKey }) });
}
export function useCancelSenderInfo() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: number) => http.post(`/account/sender-info/requests/${id}/cancel`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: senderKey }), onError: () => void qc.invalidateQueries({ queryKey: senderKey }) });
}
