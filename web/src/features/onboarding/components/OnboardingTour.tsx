import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { Button } from '@/shared/ui';
import { isOnScreen, popoverPosition, type Box, type TourId } from '../lib/tour';
import { TOURS, type TourStep } from '../steps';
import styles from './OnboardingTour.module.css';

interface OnboardingTourProps {
  /** Bộ hướng dẫn đang mở; null = đóng. */
  tour: TourId | null;
  onClose: () => void;
}

const PAD = 6;

const findTarget = (name?: string) => (name ? document.querySelector<HTMLElement>(`[data-tour="${name}"]`) : null);

const viewport = () => ({ width: window.innerWidth, height: window.innerHeight });

/** Bước có vùng chức năng trên trang lúc mở (bỏ bước của phần đang ẩn: đơn chứng từ không có kiện / invoice, chưa có đơn…). */
const availableSteps = (tour: TourId) => TOURS[tour].filter(s => !s.target || findTarget(s.target));

/** Hướng dẫn từng bước: làm tối màn hình, chiếu sáng vùng chức năng và hiện khung chú thích bên cạnh. */
export function OnboardingTour({ tour, onClose }: OnboardingTourProps) {
  const open = tour !== null;
  const navigate = useNavigate();
  const { t } = useI18n();
  const [index, setIndex] = useState(0);
  const [target, setTarget] = useState<Box | null>(null);
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);
  const [steps, setSteps] = useState<readonly TourStep[]>([]);
  const cardRef = useRef<HTMLDivElement>(null);
  const step: TourStep | undefined = steps[index];
  const last = index === steps.length - 1;

  useEffect(() => {
    const list = tour ? availableSteps(tour) : [];
    setIndex(0);
    setSteps(list);
    // Trang chưa có vùng nào của bộ hướng dẫn (đang tải…) → đóng luôn, không chặn màn hình.
    if (tour && !list.length) onClose();
  }, [tour, onClose]);

  // Đo vùng chức năng của bước hiện tại; đo lại khi đổi kích thước / cuộn.
  const measure = useCallback(() => {
    const el = findTarget(step?.target);
    const r = el?.getBoundingClientRect();
    setTarget(r ? { top: r.top, left: r.left, width: r.width, height: r.height } : null);
  }, [step?.target]);

  useEffect(() => {
    if (!open) return;
    // Vùng thấp hơn màn hình (phần cuối trang) cuộn lên giữa để còn chỗ cho khung chú thích.
    findTarget(step?.target)?.scrollIntoView({ block: tour === 'intro' ? 'nearest' : 'center' });
    measure();
    window.addEventListener('resize', measure);
    window.addEventListener('scroll', measure, true);
    return () => {
      window.removeEventListener('resize', measure);
      window.removeEventListener('scroll', measure, true);
    };
  }, [open, tour, step?.target, measure]);

  // Đặt khung chú thích sau khi biết kích thước thật của khung.
  useLayoutEffect(() => {
    const card = cardRef.current;
    if (!open || !card || !step) return;
    const { width, height } = card.getBoundingClientRect();
    const p = popoverPosition(target, { width, height }, viewport(), step.placement);
    setPos({ top: p.top, left: p.left });
    card.focus();
  }, [open, target, step, index]);

  const next = useCallback(() => (last ? onClose() : setIndex(i => i + 1)), [last, onClose]);
  const back = useCallback(() => setIndex(i => Math.max(0, i - 1)), []);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
      else if (e.key === 'ArrowRight') next();
      else if (e.key === 'ArrowLeft') back();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, next, back, onClose]);

  if (!open || !step) return null;

  const spot = isOnScreen(target, viewport()) ? target : null;

  return createPortal(
    <div className={styles.layer}>
      {/* Chặn bấm vào trang phía sau trong lúc xem hướng dẫn */}
      <div className={spot ? styles.blocker : `${styles.blocker} ${styles.dim}`} />
      {spot && (
        <div
          className={styles.spot}
          style={{ top: spot.top - PAD, left: spot.left - PAD, width: spot.width + PAD * 2, height: spot.height + PAD * 2 }}
        />
      )}
      <div
        ref={cardRef}
        className={styles.card}
        role="dialog"
        aria-modal="true"
        aria-labelledby="tour-title"
        aria-describedby="tour-body"
        tabIndex={-1}
        style={pos ? { top: pos.top, left: pos.left } : { visibility: 'hidden' }}
      >
        <div className={styles.meta}>
          {t('Bước {i}/{n}', { i: index + 1, n: steps.length })}
          <span className={styles.dots} aria-hidden="true">
            {steps.map((s, i) => <span key={s.title} className={i === index ? styles.dotOn : styles.dot} />)}
          </span>
        </div>
        <h2 id="tour-title" className={styles.title}>{t(step.title)}</h2>
        <p id="tour-body" className={styles.body}>{t(step.body)}</p>
        <div className={styles.actions}>
          {last ? (
            <Button size="sm" variant="ghost" onClick={onClose}>{t('Để sau')}</Button>
          ) : (
            <Button size="sm" variant="ghost" onClick={onClose}>{t('Bỏ qua')}</Button>
          )}
          <span className={styles.spacer} />
          {index > 0 && <Button size="sm" onClick={back}>{t('Quay lại')}</Button>}
          {last && tour === 'intro' ? (
            <Button size="sm" variant="primary" onClick={() => { onClose(); void navigate('/orders/new'); }}>{t('Tạo đơn đầu tiên')}</Button>
          ) : last ? (
            <Button size="sm" variant="primary" onClick={onClose}>{t('Xong')}</Button>
          ) : (
            <Button size="sm" variant="primary" onClick={next}>{t(index === 0 ? 'Bắt đầu' : 'Tiếp theo')}</Button>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
