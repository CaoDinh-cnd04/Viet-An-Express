import { useI18n } from '@/shared/i18n';
import { Card } from '@/shared/ui';
import { SURVEY_SECTIONS } from '@/features/feedback/constants';
import { surveyStats } from '@/features/feedback/lib/survey';
import type { AdminFeedbackItem } from '@/features/feedback/types';
import styles from './SurveyStats.module.css';

/** Thống kê khảo sát trải nghiệm: mỗi câu hỏi — số lượt trả lời, tỉ lệ chọn từng phương án, ghi chú khách viết thêm. */
export function SurveyStats({ items }: { items: AdminFeedbackItem[] }) {
  const { t } = useI18n();
  const respondents = items.filter(i => i.survey?.length).length;
  if (!respondents) return null;
  const stats = surveyStats(SURVEY_SECTIONS, items);

  return (
    <Card title={t('Thống kê khảo sát')} subtitle={t('{n} góp ý có trả lời khảo sát', { n: respondents })}>
      <div className={styles.grid}>
        {stats.map(q => (
          <section key={q.id} className={styles.question}>
            <h3 className={styles.questionText}>{t(q.question)}</h3>
            <p className={styles.muted}>{t('{n} lượt trả lời', { n: q.responses })}</p>
            <ul className={styles.options}>
              {q.options.map(o => {
                const pct = q.responses ? Math.round((o.count / q.responses) * 100) : 0;
                return (
                  <li key={o.label} className={styles.option}>
                    <span className={styles.optionLabel}>{t(o.label)}</span>
                    <span className={styles.bar} aria-hidden="true"><span style={{ width: `${pct}%` }} /></span>
                    <span className={styles.count}>{o.count} · {pct}%</span>
                  </li>
                );
              })}
            </ul>
            {q.notes.length > 0 && (
              <details className={styles.notes}>
                <summary>{t('Xem {n} ghi chú', { n: q.notes.length })}</summary>
                <ul>{q.notes.map((n, i) => <li key={i}>{n}</li>)}</ul>
              </details>
            )}
          </section>
        ))}
      </div>
    </Card>
  );
}
