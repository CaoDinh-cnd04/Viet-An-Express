import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Icon, Tabs } from '@/shared/ui';
import { SURVEY_LIMITS, SURVEY_SECTIONS, type SurveyQuestion } from '../constants';
import { isAnswered, toggleAnswer, type SurveyState } from '../lib/survey';
import styles from './Survey.module.css';

type Entry = SurveyState[string];
const EMPTY: Entry = { answers: [], note: '' };

/**
 * Khảo sát trải nghiệm ở form Góp ý: chia tab theo nhóm (mỗi lần 3–5 câu), mỗi câu bấm chọn phương án (1 hoặc nhiều);
 * ô ghi chú chỉ mở khi khách bấm "Thêm ghi chú". Không câu nào bắt buộc.
 */
export function SurveyForm({ value, onChange }: { value: SurveyState; onChange: (next: SurveyState) => void }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(true);
  const [tab, setTab] = useState(SURVEY_SECTIONS[0]!.title);
  const questions = SURVEY_SECTIONS.flatMap(s => s.questions);
  const answered = (qs: readonly SurveyQuestion[]) => qs.filter(q => isAnswered(value[q.id])).length;
  const index = Math.max(0, SURVEY_SECTIONS.findIndex(s => s.title === tab));
  const section = SURVEY_SECTIONS[index]!;
  const next = SURVEY_SECTIONS[index + 1];

  return (
    <section className={styles.survey}>
      <button type="button" className={styles.toggle} aria-expanded={open} onClick={() => setOpen(!open)}>
        <span className={styles.title}>{t('Khảo sát trải nghiệm (không bắt buộc)')}</span>
        <span className={styles.progress}>
          {t('đã trả lời {n}/{total} câu', { n: answered(questions), total: questions.length })}
          <Icon name={open ? 'chevronDown' : 'chevronRight'} size={16} />
        </span>
      </button>

      {open && (
        <>
          <Tabs
            ariaLabel="Nhóm câu hỏi khảo sát"
            items={SURVEY_SECTIONS.map(s => ({ key: s.title, label: s.title, count: answered(s.questions) }))}
            value={section.title}
            onChange={setTab}
          />
          <ol className={styles.questions}>
            {section.questions.map(q => (
              <SurveyQuestionItem
                key={q.id}
                q={q}
                entry={value[q.id] ?? EMPTY}
                onChange={entry => onChange({ ...value, [q.id]: entry })}
              />
            ))}
          </ol>
          {next && (
            <div className={styles.next}>
              <button type="button" className={styles.linkButton} onClick={() => setTab(next.title)}>
                {t('Nhóm tiếp theo: {name}', { name: t(next.title) })} <Icon name="chevronRight" size={14} />
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function SurveyQuestionItem({ q, entry, onChange }: { q: SurveyQuestion; entry: Entry; onChange: (entry: Entry) => void }) {
  const { t } = useI18n();
  const [noteOpen, setNoteOpen] = useState(false);
  const showNote = !!q.notePlaceholder && (noteOpen || entry.note.length > 0);

  return (
    <li className={styles.question}>
      <fieldset className={styles.fieldset}>
        <legend className={styles.questionText}>
          {t(q.question)}
          {q.multiple && <span className={styles.hint}> · {t('chọn được nhiều')}</span>}
        </legend>
        <div className={styles.options}>
          {q.options.map(o => {
            const on = entry.answers.includes(o.label);
            return (
              <button
                key={o.label}
                type="button"
                className={on ? `${styles.option} ${styles.optionOn}` : styles.option}
                aria-pressed={on}
                onClick={() => onChange({ ...entry, answers: toggleAnswer(q, entry.answers, o.label) })}
              >
                {on && <Icon name="check" size={13} />}
                {t(o.label)}
              </button>
            );
          })}
          {q.notePlaceholder && !showNote && (
            <button type="button" className={styles.linkButton} onClick={() => setNoteOpen(true)}>
              <Icon name="plus" size={13} /> {t('Thêm ghi chú')}
            </button>
          )}
        </div>
        {showNote && (
          <input
            className={styles.note}
            autoFocus={noteOpen && !entry.note}
            maxLength={SURVEY_LIMITS.noteMax}
            placeholder={t(q.notePlaceholder!)}
            aria-label={t('Ghi chú cho câu: {q}', { q: t(q.question) })}
            value={entry.note}
            onChange={e => onChange({ ...entry, note: e.target.value })}
          />
        )}
      </fieldset>
    </li>
  );
}
