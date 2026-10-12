import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/http';

/** Thông báo — docs/API_CONTRACT.md §6. */
export interface Notification {
  id: number | string;
  /** Quan trọng → hiện popup khi mở portal. */
  imp: boolean;
  unread: boolean;
  title: string;
  date: string;
  /** Nội dung nhiều dòng (\n). */
  body: string;
  /** Đường link "Xem tại đây" (http / https), không bắt buộc. */
  link?: string | null;
}

interface NotificationListResponse {
  data: Notification[];
  unreadCount: number;
}

export const notificationKeys = { all: ['notifications'] as const };

export function useNotifications() {
  return useQuery({
    queryKey: notificationKeys.all,
    queryFn: () => http.get<NotificationListResponse>('/notifications'),
    refetchInterval: 5 * 60_000
  });
}

export function useMarkRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: Notification['id']) => http.post(`/notifications/${encodeURIComponent(String(id))}/read`),
    // Cập nhật ngay trên cache để badge giảm tức thì
    onMutate: id =>
      qc.setQueryData<NotificationListResponse>(notificationKeys.all, prev =>
        prev && {
          data: prev.data.map(n => (n.id === id ? { ...n, unread: false } : n)),
          unreadCount: Math.max(0, prev.unreadCount - (prev.data.find(n => n.id === id)?.unread ? 1 : 0))
        }
      ),
    onSettled: () => void qc.invalidateQueries({ queryKey: notificationKeys.all })
  });
}

export function useMarkAllRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => http.post('/notifications/mark-all-read'),
    onSuccess: () => void qc.invalidateQueries({ queryKey: notificationKeys.all })
  });
}
