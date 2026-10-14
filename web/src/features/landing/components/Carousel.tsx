import { useCallback, useEffect, useRef, useState, type PointerEvent, type ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import { activeIndex, nextLeft, prevLeft, type ScrollState } from '../lib/carousel';
import styles from './Carousel.module.css';

interface CarouselProps {
  items: ReadonlyArray<{ key: string; node: ReactNode }>;
  ariaLabel: string;
  /** Thời gian mỗi lần tự chuyển (ms). */
  interval?: number;
}

/** Sau khi khách tự thao tác, chờ bấy lâu rồi mới tự chuyển lại. */
const RESUME_AFTER = 6000;
/** Kéo chuột quá bấy nhiêu px thì coi là kéo, không phải bấm. */
const DRAG_THRESHOLD = 6;

/**
 * Carousel 1 hàng: tự chuyển từng thẻ, tới cuối quay về đầu.
 * Khách vuốt (cảm ứng) / kéo chuột / bấm nút ‹ › / bấm chấm để tới vị trí mong muốn.
 * Dựa trên cuộn ngang gốc của trình duyệt + scroll-snap, nên bàn phím và trackpad cũng dùng được.
 */
export function Carousel({ items, ariaLabel, interval = 3500 }: CarouselProps) {
  const { t } = useI18n();
  const viewport = useRef<HTMLUListElement>(null);
  const [state, setState] = useState<ScrollState>({ left: 0, max: 0, step: 0 });
  const [hovered, setHovered] = useState(false);
  const [dragging, setDragging] = useState(false);
  const pausedUntil = useRef(0);
  const drag = useRef<{ x: number; left: number; moved: boolean } | null>(null);

  const measure = useCallback(() => {
    const el = viewport.current;
    const first = el?.firstElementChild as HTMLElement | null;
    if (!el || !first) return;
    const gap = parseFloat(getComputedStyle(el).columnGap) || 0;
    setState({ left: el.scrollLeft, max: el.scrollWidth - el.clientWidth, step: first.offsetWidth + gap });
  }, []);

  useEffect(() => {
    measure();
    const el = viewport.current;
    if (!el) return;
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    el.addEventListener('scroll', measure, { passive: true });
    return () => {
      ro.disconnect();
      el.removeEventListener('scroll', measure);
    };
  }, [measure]);

  const go = useCallback((left: number) => viewport.current?.scrollTo({ left, behavior: 'smooth' }), []);
  const userAction = () => (pausedUntil.current = Date.now() + RESUME_AFTER);

  // Tự chuyển: dừng khi rê chuột, đang kéo, tab ẩn, hoặc khách vừa thao tác.
  useEffect(() => {
    if (hovered || dragging) return;
    const id = window.setInterval(() => {
      const el = viewport.current;
      if (!el || document.hidden || Date.now() < pausedUntil.current) return;
      if (el.contains(document.activeElement)) return;
      const first = el.firstElementChild as HTMLElement | null;
      const gap = parseFloat(getComputedStyle(el).columnGap) || 0;
      go(nextLeft({ left: el.scrollLeft, max: el.scrollWidth - el.clientWidth, step: (first?.offsetWidth ?? 0) + gap }));
    }, interval);
    return () => window.clearInterval(id);
  }, [hovered, dragging, interval, go]);

  // Kéo bằng chuột (cảm ứng đã có vuốt gốc của trình duyệt).
  const onPointerDown = (e: PointerEvent<HTMLUListElement>) => {
    if (e.pointerType !== 'mouse' || e.button !== 0 || !viewport.current) return;
    drag.current = { x: e.clientX, left: viewport.current.scrollLeft, moved: false };
  };
  const onPointerMove = (e: PointerEvent<HTMLUListElement>) => {
    const d = drag.current;
    const el = viewport.current;
    if (!d || !el) return;
    const dx = e.clientX - d.x;
    if (!d.moved && Math.abs(dx) < DRAG_THRESHOLD) return;
    if (!d.moved) {
      d.moved = true;
      setDragging(true);
      try {
        el.setPointerCapture(e.pointerId); // giữ kéo khi chuột ra ngoài khung
      } catch {
        /* con trỏ đã nhả — bỏ qua */
      }
    }
    el.scrollLeft = d.left - dx;
  };
  const endDrag = () => {
    if (drag.current?.moved) {
      userAction();
      setDragging(false);
      // Nhả chuột: bám vào thẻ gần nhất.
      const { step, max } = state;
      const el = viewport.current;
      if (el && step > 0) go(Math.min(max, Math.round(el.scrollLeft / step) * step));
    }
    // Giữ cờ `moved` tới sau sự kiện click để chặn bấm nhầm link khi vừa kéo.
    window.setTimeout(() => (drag.current = null), 0);
  };

  const pageCount = state.step > 0 && state.max > 0 ? Math.ceil(state.max / state.step - 0.05) + 1 : 1;
  const current = activeIndex(state, pageCount);

  return (
    <div
      className={styles.carousel}
      role="region"
      aria-roledescription="carousel"
      aria-label={ariaLabel}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <ul
        ref={viewport}
        className={cx(styles.viewport, dragging && styles.dragging)}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        onClickCapture={e => {
          if (drag.current?.moved) {
            e.preventDefault();
            e.stopPropagation();
          }
        }}
        onTouchStart={userAction}
        onWheel={userAction}
        onDragStart={e => e.preventDefault()}
      >
        {items.map(item => (
          <li key={item.key} className={styles.slide}>
            {item.node}
          </li>
        ))}
      </ul>

      {pageCount > 1 && (
        <div className={styles.controls}>
          <button
            type="button"
            className={styles.arrow}
            aria-label={t('Xem thẻ trước')}
            onClick={() => {
              userAction();
              go(prevLeft(state));
            }}
          >
            <Icon name="chevronRight" size={20} className={styles.flip} />
          </button>
          <div className={styles.dots}>
            {Array.from({ length: pageCount }, (_, i) => (
              <button
                key={i}
                type="button"
                className={cx(styles.dot, i === current && styles.dotActive)}
                aria-label={t('Tới vị trí {n}', { n: i + 1 })}
                aria-current={i === current}
                onClick={() => {
                  userAction();
                  go(Math.min(state.max, i * state.step));
                }}
              />
            ))}
          </div>
          <button
            type="button"
            className={styles.arrow}
            aria-label={t('Xem thẻ tiếp theo')}
            onClick={() => {
              userAction();
              go(nextLeft(state));
            }}
          >
            <Icon name="chevronRight" size={20} />
          </button>
        </div>
      )}
    </div>
  );
}
