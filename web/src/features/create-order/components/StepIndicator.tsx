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
  /** Bước nào đã điền đủ các trường bắt buộc (theo kiểm tra form) — quyết định "Đã xong" / "Còn thiếu" và thanh tiến độ. */
  completed?: ReadonlyArray<boolean>;
}

/** Nhãn trạng thái dưới mỗi bước: xong = đủ trường bắt buộc; còn thiếu = đã qua nhưng chưa đủ. */
const STATE_LABEL = { passed: 'Đã xong', active: 'Đang điền', missing: 'Còn thiếu', todo: 'Chưa làm' } as const;

/**
 * Dòng thời gian dọc các bước tạo đơn (cao vừa nội dung): mỗi bước có tên, mô tả
 * và trạng thái (Đã xong / Đang điền / Chưa làm); đáy khung là thanh tiến độ.
 * Bấm bước để chuyển bước (từng bước) hoặc cuộn tới phần đó (1 trang).
 */
export function StepIndicator({ steps, current, onSelectStep, showDone = true, completed = [] }: StepIndicatorProps) {
  const { t } = useI18n();
  const doneCount = steps.filter((_, i) => completed[i] === true).length;
  return (
    <nav className={styles.panel} aria-label={t('Các bước tạo đơn')}>
      <ol className={styles.timeline}>
        {steps.map((s, i) => {
          // Trạng thái theo dữ liệu thật: bước đang xem = Đang điền; bước khác đủ trường = Đã xong, đã qua mà thiếu = Còn thiếu
          const complete = completed[i] === true;
          const state = i === current ? 'active' : complete ? 'passed' : i < current ? 'missing' : 'todo';
          const done = showDone && complete && i !== current;
          return (
            <li key={s.title} className={cx(styles.step, styles[state])}>
              <button type="button" className={styles.head} onClick={() => onSelectStep(i)} aria-current={state === 'active' ? 'step' : undefined}>
                <span className={cx(styles.num, done && styles.numDone)}>{done ? <Icon name="check" size={14} /> : i + 1}</span>
                <span className={styles.text}>
                  <strong>{t(s.title)}</strong>
                  <small>{t(s.description)}</small>
                  <span className={styles.state}>{t(STATE_LABEL[state])}</span>
                </span>
              </button>
            </li>
          );
        })}
      </ol>
      <div className={styles.progress}>
        <span>{t('Hoàn thành {done}/{total} bước', { done: doneCount, total: steps.length })}</span>
        <span className={styles.track} aria-hidden="true">
          <i style={{ width: `${Math.max(4, (doneCount / steps.length) * 100)}%` }} />
        </span>
      </div>
    </nav>
  );
}
