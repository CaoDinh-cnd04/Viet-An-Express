import { describe, expect, it } from 'vitest';
import { isOnScreen, pageTourId, popoverPosition, tourStorageKey } from './tour';

const viewport = { width: 1440, height: 900 };
const size = { width: 340, height: 200 };

describe('popoverPosition', () => {
  it('menu bên trái: khung nằm bên phải, cùng hàng', () => {
    const p = popoverPosition({ top: 120, left: 12, width: 220, height: 40 }, size, viewport, 'right');
    expect(p).toEqual({ top: 120, left: 246, placement: 'right' });
  });

  it('mục ở cuối menu: khung đẩy lên để không tràn đáy màn hình', () => {
    const p = popoverPosition({ top: 820, left: 12, width: 220, height: 40 }, size, viewport, 'right');
    expect(p.top).toBe(900 - 200 - 12);
  });

  it('nút góc phải thanh trên cùng: khung nằm dưới, canh mép phải', () => {
    const p = popoverPosition({ top: 12, left: 1380, width: 36, height: 36 }, size, viewport, 'bottom');
    expect(p).toEqual({ top: 62, left: 1380 + 36 - 340, placement: 'bottom' });
  });

  it('không đủ chỗ bên phải thì chuyển xuống dưới', () => {
    const p = popoverPosition({ top: 100, left: 1200, width: 200, height: 40 }, size, viewport, 'right');
    expect(p.placement).toBe('bottom');
    expect(p.left).toBeLessThanOrEqual(1440 - 340 - 12);
  });

  it('vùng ở sát đáy màn hình: khung nằm phía trên vùng đó', () => {
    const p = popoverPosition({ top: 820, left: 400, width: 300, height: 40 }, size, viewport, 'bottom');
    expect(p.top).toBe(820 - 14 - 200);
  });

  it('không có vùng chức năng / vùng bị ẩn → giữa màn hình', () => {
    expect(popoverPosition(null, size, viewport, 'right')).toEqual({ top: 350, left: 550, placement: 'center' });
    expect(popoverPosition({ top: 100, left: -260, width: 240, height: 40 }, size, viewport, 'right').placement).toBe('center');
  });
});

describe('isOnScreen', () => {
  it('menu bị ẩn (kích thước 0 hoặc nằm ngoài màn hình) thì không chỉ vào', () => {
    expect(isOnScreen({ top: 0, left: 0, width: 0, height: 0 }, viewport)).toBe(false);
    expect(isOnScreen({ top: 10, left: -300, width: 260, height: 40 }, viewport)).toBe(false);
    expect(isOnScreen({ top: 10, left: 10, width: 260, height: 40 }, viewport)).toBe(true);
  });
});

it('mỗi khách một khoá lưu riêng; hướng dẫn tổng quát giữ khoá cũ, mỗi trang 1 khoá', () => {
  expect(tourStorageKey('SCS')).toBe('va.tour.v1.SCS');
  expect(tourStorageKey('')).toBe('va.tour.v1.guest');
  expect(tourStorageKey('SCS', 'orders')).toBe('va.tour.orders.v1.SCS');
});

it('chọn hướng dẫn theo trang đang mở', () => {
  expect(pageTourId('/orders/new/quick')).toBe('create-quick');
  expect(pageTourId('/orders/')).toBe('orders');
  expect(pageTourId('/orders/new')).toBeUndefined();
  expect(pageTourId('/')).toBeUndefined();
});
