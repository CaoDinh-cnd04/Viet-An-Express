import { PERMISSIONS } from '@/features/auth';
import type { IconName } from '@/shared/ui';

/** Khóa badge số đếm trên menu — giá trị lấy từ useNavBadges. */
export type BadgeKey = 'drafts' | 'troubles' | 'notifications';

export interface NavLinkItem {
  to: string;
  label: string;
  badge?: BadgeKey;
  /** Quyền cần có — không có thì ẩn khỏi menu (tài khoản con của nhân viên). */
  permission?: string;
}

export type NavEntry =
  | ({ kind: 'link'; icon: IconName } & NavLinkItem)
  | {
      kind: 'group';
      id: string;
      label: string;
      /** Tên ngắn dưới icon khi sidebar thu gọn (1 dòng, ~10 ký tự). */
      short: string;
      icon: IconName;
      children: NavLinkItem[];
    };

/**
 * Menu sidebar. Thêm trang mới: thêm route trong app/routes.tsx rồi thêm 1 dòng ở đây.
 */
export const NAV: ReadonlyArray<NavEntry> = [
  {
    kind: 'group',
    id: 'create',
    label: 'Tạo đơn',
    short: 'Tạo đơn',
    icon: 'filePlus',
    children: [
      { to: '/orders/new', label: 'Tạo đơn từng bước', permission: PERMISSIONS.shipmentsCreate },
      { to: '/orders/new/quick', label: 'Tạo đơn 1 trang', permission: PERMISSIONS.shipmentsCreate },
      { to: '/orders/import', label: 'Tạo đơn từ Excel', permission: PERMISSIONS.shipmentsCreate }
    ]
  },
  {
    kind: 'group',
    id: 'orders',
    label: 'Quản lý đơn hàng',
    short: 'Đơn hàng',
    icon: 'box',
    children: [
      { to: '/drafts', label: 'Đơn nháp & chưa in', badge: 'drafts', permission: PERMISSIONS.shipmentsView },
      { to: '/orders', label: 'Đơn hàng của tôi', permission: PERMISSIONS.shipmentsView }
    ]
  },
  {
    kind: 'group',
    id: 'sales',
    label: 'Dịch vụ & Bán hàng',
    short: 'Bán hàng',
    icon: 'bag',
    children: [
      { to: '/pricing', label: 'Giá & gợi ý dịch vụ' },
      { to: '/ecommerce', label: 'E-commerce', permission: PERMISSIONS.ecommerceView },
      { to: '/ecommerce/orders', label: 'Đơn hàng E-com', permission: PERMISSIONS.ecommerceView }
    ]
  },
  {
    kind: 'group',
    id: 'support',
    label: 'Hỗ trợ',
    short: 'Hỗ trợ',
    icon: 'help',
    children: [
      { to: '/troubles', label: 'Hỗ trợ & sự cố', badge: 'troubles' },
      { to: '/notifications', label: 'Thông báo', badge: 'notifications' }
    ]
  },
  {
    kind: 'group',
    id: 'account',
    label: 'Tài khoản',
    short: 'Tài khoản',
    icon: 'user',
    children: [
      { to: '/account/mytracking', label: 'MyTracking cá nhân', permission: PERMISSIONS.myTracking },
      { to: '/account/staff', label: 'Tài khoản nhân viên', permission: PERMISSIONS.manageStaff }
    ]
  }
];
