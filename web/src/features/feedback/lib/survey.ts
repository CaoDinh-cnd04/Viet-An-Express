import type { SurveyQuestion, SurveySection } from '../constants';
import type { SurveyAnswer } from '../types';

/** Câu trả lời đang nhập trên form, theo mã câu hỏi. */
export type SurveyState = Record<string, { answers: string[]; note: string }>;

/**
 * Bấm 1 phương án: đang chọn thì bỏ; câu chọn 1 thì thay; phương án `exclusive` thì chỉ còn nó; phương án thường thì bỏ các phương án `exclusive`.
 * Kết quả xếp theo thứ tự phương án của câu hỏi. Hàm thuần — có test.
 */
export function toggleAnswer(q: SurveyQuestion, current: readonly string[], label: string): string[] {
  let next: string[];
  if (current.includes(label)) next = current.filter(a => a !== label);
  else if (!q.multiple || q.options.find(o => o.label === label)?.exclusive) next = [label];
  else next = [...current.filter(a => !q.options.find(o => o.label === a)?.exclusive), label];
  const order = (a: string) => q.options.findIndex(o => o.label === a);
  return next.sort((a, b) => order(a) - order(b));
}

/** Câu đã trả lời (chọn ít nhất 1 phương án hoặc có ghi chú). */
export function isAnswered(entry: SurveyState[string] | undefined): boolean {
  return !!entry && (entry.answers.length > 0 || entry.note.trim().length > 0);
}

/** Dữ liệu gửi backend: chỉ các câu đã trả lời, kèm nội dung câu hỏi (tiếng Việt gốc), theo thứ tự trên form. */
export function surveyPayload(sections: readonly SurveySection[], state: SurveyState): SurveyAnswer[] {
  return sections.flatMap(s => s.questions).flatMap(q => {
    const entry = state[q.id];
    if (!entry || !isAnswered(entry)) return [];
    return [{ id: q.id, question: q.question, answers: entry.answers, note: entry.note.trim() || null }];
  });
}

export interface SurveyQuestionStats {
  id: string;
  question: string;
  /** Số góp ý trả lời câu này. */
  responses: number;
  options: { label: string; count: number }[];
  /** Ghi chú khách viết thêm (mới nhất trước — theo thứ tự danh sách góp ý). */
  notes: string[];
}

/** Thống kê khảo sát cho trang quản trị: số lượt chọn từng phương án của bộ câu hỏi hiện tại. Hàm thuần — có test. */
export function surveyStats(
  sections: readonly SurveySection[],
  items: ReadonlyArray<{ survey?: SurveyAnswer[] | null }>
): SurveyQuestionStats[] {
  return sections.flatMap(s => s.questions).map(q => {
    const answers = items.flatMap(i => (i.survey ?? []).filter(a => a.id === q.id));
    return {
      id: q.id,
      question: q.question,
      responses: answers.length,
      options: q.options.map(o => ({ label: o.label, count: answers.filter(a => a.answers.includes(o.label)).length })),
      notes: answers.map(a => a.note?.trim() ?? '').filter(Boolean)
    };
  });
}
