import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiUrl, http } from '@/shared/api/http';

/**
 * 1 tin trong thông báo: tiêu đề (không bắt buộc khi chỉ có 1 tin), nội dung (chữ thường hoặc HTML có định dạng), ảnh (tên tệp)
 * và nút "Xem chi tiết" mở link hoặc mở nội dung chi tiết admin soạn (detail).
 */
export interface NotificationItem {
  title?: string | null;
  body: string;
  images: string[];
  link?: string | null;
  detail?: string | null;
}

/** Thông báo — docs/API_CONTRACT.md §6. */
export interface Notification {
  id: number | string;
  /** Quan trọng → hiện popup khi mở portal. */
  imp: boolean;
  /** Ghim lên đầu danh sách. */
  pin: boolean;
  unread: boolean;
  /** notice: Việt An đăng · support: cập nhật hỗ trợ & sự cố gửi riêng khách. */
  kind: 'notice' | 'support';
  title: string;
  /** 'dd/MM/yyyy HH:mm' — giờ đăng. */
  date: string;
  /** Bản chữ tóm tắt; thông báo cũ / hỗ trợ chỉ có body + link (items trống). */
  body: string;
  link?: string | null;
  items: NotificationItem[];
}

interface NotificationListResponse {
  data: Notification[];
  unreadCount: number;
}

export const notificationKeys = { all: ['notifications'] as const };

/** Link ảnh của tin (API công khai, cache lâu vì tên tệp theo mã băm). */
export const notificationImageUrl = (name: string) => apiUrl(`/public/notifications/images/${encodeURIComponent(name)}`);

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
