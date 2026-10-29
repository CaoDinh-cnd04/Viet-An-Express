import { useEffect, useState } from 'react';

/** Khoảng cách từ đỉnh màn hình (dưới thanh trên cùng) để tính khung "đang xem". */
const SPY_LINE = 140;

/** Khung đang xem: khung cuối cùng đã cuộn qua vạch SPY_LINE; cuộn hết trang thì là khung cuối có trên trang. */
export function useActiveSection(ids: readonly string[]) {
  const [active, setActive] = useState<string>();
  const key = ids.join('|');
  useEffect(() => {
    const list = key.split('|');
    const update = () => {
      const present = list.filter(id => document.getElementById(id));
      let current = present[0];
      for (const id of present) if (document.getElementById(id)!.getBoundingClientRect().top <= SPY_LINE) current = id;
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
