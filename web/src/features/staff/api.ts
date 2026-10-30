import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getErrorMessage, http } from '@/shared/api/http';
import { useToast } from '@/shared/ui';
import type { NewStaff, StaffAccount, StaffUpdate } from './types';

const staffKey = ['account-staff'] as const;

/** Danh sách tài khoản nhân viên của công ty — chỉ tài khoản chính (admin). */
export function useStaffList() {
  return useQuery({
    queryKey: staffKey,
    queryFn: () => http.get<{ data: StaffAccount[] }>('/account/staff').then(r => r.data),
    retry: false
  });
}

/** Thêm / sửa / đặt lại mật khẩu / xóa: báo kết quả bằng toast, tải lại danh sách. */
function useStaffMutation<V>(fn: (vars: V) => Promise<{ message?: string }>) {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: fn,
    onSuccess: res => {
      if (res.message) toast.show(res.message, 'success');
      void qc.invalidateQueries({ queryKey: staffKey });
    },
    onError: e => toast.show(getErrorMessage(e), 'error')
  });
}

export const useCreateStaff = () =>
  useStaffMutation((body: NewStaff) => http.post<{ data: StaffAccount; message?: string }>('/account/staff', body));

export const useUpdateStaff = () =>
  useStaffMutation(({ id, body }: { id: number; body: StaffUpdate }) =>
    http.put<{ data: StaffAccount; message?: string }>(`/account/staff/${id}`, body));

export const useResetStaffPassword = () =>
  useStaffMutation(({ id, newPassword }: { id: number; newPassword: string }) =>
    http.post<{ message?: string }>(`/account/staff/${id}/reset-password`, { newPassword }));

export const useDeleteStaff = () => useStaffMutation((id: number) => http.delete<{ message?: string }>(`/account/staff/${id}`));
