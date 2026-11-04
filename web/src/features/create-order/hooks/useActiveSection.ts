import { useEffect, useState } from 'react';

/** Khoảng cách từ đỉnh màn hình để tính khung "đang xem" — dưới thanh trên cùng + thanh các bước đang ghim. */
const SPY_LINE = 170;

/**
 * Khung đang xem: trong các khung đã cuộn qua vạch SPY_LINE, khung có đỉnh thấp nhất (gần vạch nhất); 2 khung cùng hàng
 * (người gửi | người nhận) thì lấy khung đứng trước trong danh sách. Cuộn hết trang thì là khung cuối có trên trang.
 */
export function useActiveSection(ids: readonly string[]) {
  const [active, setActive] = useState<string>();
  const key = ids.join('|');
  useEffect(() => {
    const list = key.split('|');
    const update = () => {
      const present = list.filter(id => document.getElementById(id));
      let current = present[0];
      let currentTop = -Infinity;
      for (const id of present) {
        const top = document.getElementById(id)!.getBoundingClientRect().top;
        if (top <= SPY_LINE && top > currentTop) {
          current = id;
          currentTop = top;
        }
      }
      const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4;
      setActive(atBottom ? present[present.length - 1] : current);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    return () => {
      window.removeEventListener('scroll', update);
      window.removeEventListener('resize', update);
    };
  }, [key]);
  return active;
}
