import { useI18n } from '@/shared/i18n';
import type { SurveyAnswer } from '../types';
import styles from './Survey.module.css';

/** Câu trả lời khảo sát đã gửi (trang Góp ý của khách, chi tiết góp ý ở trang quản trị). */
export function SurveyAnswers({ survey }: { survey?: SurveyAnswer[] | null }) {
  const { t } = useI18n();
  if (!survey?.length) return null;
  return (
    <dl className={styles.answers}>
      {survey.map(a => (
        <div key={a.id} className={styles.answer}>
          <dt>{t(a.question)}</dt>
          <dd>
            {a.answers.map(x => t(x)).join(' · ')}
            {a.note && <span className={styles.answerNote}>{a.answers.length ? ' — ' : ''}{a.note}</span>}
          </dd>
        </div>
      ))}
    </dl>
  );
}
