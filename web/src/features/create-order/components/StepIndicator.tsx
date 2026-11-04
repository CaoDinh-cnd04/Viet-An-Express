import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import type { WizardStep } from '../constants';
import styles from './StepIndicator.module.css';

/** 1 khung con trong 1 bước (vd "Người gửi") — `id` là id phần tử trên trang để cuộn tới. */
export interface StepSection {
  id: string;
  label: string;
}

interface StepIndicatorProps {
  steps: ReadonlyArray<WizardStep>;
  /** Không còn hiện khung con (chỉ hiện bước chính) — giữ để trang gọi không phải đổi. */
  sections?: ReadonlyArray<ReadonlyArray<StepSection>>;
  /** Bước đang làm / đang xem. */
  current: number;
  activeSection?: string;
  onSelectStep: (step: number) => void;
  onSelectSection?: (step: number, sectionId: string) => void;
  /** Hiện dấu tích ở bước đã xong. */
  showDone?: boolean;
  /** Bước nào đã điền đủ các trường bắt buộc (theo kiểm tra form) — quyết định "Đã xong" / "Còn thiếu". */
  completed?: ReadonlyArray<boolean>;
}

/** Trạng thái từng bước — hiện ở chú thích khi rê chuột; chỉ "Còn thiếu" hiện thành chữ. */
const STATE_LABEL = { passed: 'Đã xong', active: 'Đang điền', missing: 'Còn thiếu', todo: 'Chưa làm' } as const;

/**
 * Thanh các bước tạo đơn nằm ngang trên đầu trang (cột trái chỉ còn khung Hỗ trợ khách hàng): số + tên bước,
 * trạng thái bằng màu (đang làm · đã xong có dấu tích · đã qua mà còn thiếu thì nhắc "Còn thiếu").
 * Bấm bước để chuyển bước (từng bước) hoặc cuộn tới phần đó (1 trang).
 */
export function StepIndicator({ steps, current, onSelectStep, showDone = true, completed = [] }: StepIndicatorProps) {
  const { t } = useI18n();
  return (
    <nav className={styles.bar} aria-label={t('Các bước tạo đơn')}>
      <ol className={styles.steps}>
        {steps.map((s, i) => {
          const complete = completed[i] === true;
          const state = i === current ? 'active' : complete ? 'passed' : i < current ? 'missing' : 'todo';
          const done = showDone && complete && i !== current;
          return (
            <li key={s.title} className={cx(styles.step, styles[state])}>
              <button type="button" className={styles.head} onClick={() => onSelectStep(i)} aria-current={state === 'active' ? 'step' : undefined}
                title={`${t(s.description)} · ${t(STATE_LABEL[state])}`}>
                <span className={styles.num}>{done ? <Icon name="check" size={13} /> : i + 1}</span>
                <span className={styles.title}>{t(s.title)}</span>
                {state === 'missing' && <span className={styles.missingText}>{t(STATE_LABEL.missing)}</span>}
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
