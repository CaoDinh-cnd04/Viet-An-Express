import { useDrafts } from '@/features/drafts';
import { useNotifications } from '@/features/notifications';
import { useSupportTickets } from '@/features/support';
import type { BadgeKey } from './nav.config';

/** Số đếm hiển thị trên menu: đơn nháp, sự cố chưa xử lý, thông báo chưa đọc. Dùng chung cache với các trang. */
export function useNavBadges(): Record<BadgeKey, number> {
  const drafts = useDrafts();
  const support = useSupportTickets();
  const notifications = useNotifications();

  return {
    drafts: drafts.data?.length ?? 0,
    support: (support.data ?? []).filter(t => t.status !== 'done').length,
    notifications: notifications.data?.unreadCount ?? 0
  };
}
