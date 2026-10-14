import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '@/shared/api/http';
import { adminFetch } from './api';
import type { AdminSenderRequest, SenderStatus } from '@/features/sender-info/types';
export const adminSenderKey = ['admin', 'sender-info'] as const;
export function useAdminSenderInfo(enabled = true, status: SenderStatus | 'all' = 'pending') {
  return useQuery({ queryKey: [...adminSenderKey, status], enabled,
    queryFn: async () => ((await (await adminFetch(`/admin/sender-info${status === 'all' ? '' : `?status=${status}`}`)).json()) as { data: AdminSenderRequest[] }).data,
    refetchInterval: enabled ? 30_000 : false, retry: (count, e) => !(e instanceof ApiError && [401, 403].includes(e.status)) && count < 1 });
}
export function useReviewSenderInfo() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: async ({ id, action, reason }: { id: number; action: 'approve' | 'reject'; reason?: string }) =>
    ((await (await adminFetch(`/admin/sender-info/${id}/${action}`, { method: 'POST', ...(action === 'reject' ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason }) } : {}) })).json()) as { data: AdminSenderRequest }).data,
    onSuccess: () => void qc.invalidateQueries({ queryKey: adminSenderKey }), onError: () => void qc.invalidateQueries({ queryKey: adminSenderKey }) });
}
