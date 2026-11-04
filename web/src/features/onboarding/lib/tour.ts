/** Hình chữ nhật trên màn hình (giống DOMRect, dùng được trong test). */
export interface Box {
  top: number;
  left: number;
  width: number;
  height: number;
}

export type Placement = 'right' | 'bottom' | 'center';

/** intro = hướng dẫn tổng quát lần đầu đăng nhập; còn lại = hướng dẫn từng nút của 1 trang. */
export type TourId = 'intro' | 'create-quick' | 'orders' | 'support' | 'mytracking' | 'staff';

/** Trang có hướng dẫn riêng (đường dẫn → bộ hướng dẫn). */
const PAGE_TOURS: Readonly<Record<string, TourId>> = {
  '/orders/new/quick': 'create-quick',
  '/orders': 'orders',
  '/troubles': 'support',
  '/account/mytracking': 'mytracking',
  '/account/staff': 'staff'
};

/** Bộ hướng dẫn của trang đang mở (bỏ "/" cuối); trang không có hướng dẫn riêng → undefined. */
export const pageTourId = (pathname: string): TourId | undefined => PAGE_TOURS[pathname.replace(/(.)\/+$/, '$1')];

/**
 * Đã xem hướng dẫn — lưu theo từng khách trên trình duyệt (tăng v khi đổi nội dung để khách xem lại).
 * Hướng dẫn tổng quát giữ khoá cũ để khách đã xem không phải xem lại.
 */
export const tourStorageKey = (customerCode: string, id: TourId = 'intro') =>
  id === 'intro' ? `va.tour.v1.${customerCode || 'guest'}` : `va.tour.${id}.v1.${customerCode || 'guest'}`;

const GAP = 14;
const MARGIN = 12;

/** Vùng chức năng có đang hiện trên màn hình không (menu thu gọn / ẩn trên điện thoại thì không). */
export function isOnScreen(target: Box | null, viewport: { width: number; height: number }): target is Box {
  return !!target && target.width > 0 && target.height > 0 && target.left >= 0 && target.left + target.width <= viewport.width;
}

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), Math.max(min, max));

/**
 * Vị trí khung chú thích: bên phải vùng chức năng (menu trái) hoặc bên dưới (thanh trên cùng),
 * hết chỗ thì đổi phía (phải → dưới → trên); luôn nằm trong màn hình. Không có vùng chức năng → giữa màn hình.
 */
export function popoverPosition(
  target: Box | null,
  size: { width: number; height: number },
  viewport: { width: number; height: number },
  preferred: Placement
): { top: number; left: number; placement: Placement } {
  const center = {
    top: Math.max(MARGIN, (viewport.height - size.height) / 2),
    left: Math.max(MARGIN, (viewport.width - size.width) / 2),
    placement: 'center' as const
  };
  if (preferred === 'center' || !isOnScreen(target, viewport)) return center;

  const maxLeft = viewport.width - size.width - MARGIN;
  const maxTop = viewport.height - size.height - MARGIN;
  const rightFits = target.left + target.width + GAP + size.width + MARGIN <= viewport.width;

  if (preferred === 'right' && rightFits) {
    return { top: clamp(target.top, MARGIN, maxTop), left: target.left + target.width + GAP, placement: 'right' };
  }
  const below = target.top + target.height + GAP;
  if (below + size.height + MARGIN <= viewport.height) {
    // Canh mép phải khung theo mép phải vùng chức năng (nút ở góc phải thanh trên cùng).
    return { top: below, left: clamp(target.left + target.width - size.width, MARGIN, maxLeft), placement: 'bottom' };
  }
  // Vùng ở thấp trên màn hình (nút cuối trang): khung nằm phía trên.
  const above = target.top - GAP - size.height;
  if (above >= MARGIN) {
    return { top: above, left: clamp(target.left + target.width - size.width, MARGIN, maxLeft), placement: 'bottom' };
  }
  return center;
}
