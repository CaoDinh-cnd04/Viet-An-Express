import { describe, expect, it } from 'vitest';
import { SURVEY_SECTIONS, type SurveyQuestion, type SurveySection } from '../constants';
import { isAnswered, surveyPayload, surveyStats, toggleAnswer } from './survey';

const single: SurveyQuestion = { id: 'mobile', question: 'Điện thoại?', options: [{ label: 'Ổn' }, { label: 'Khó' }] };
const multi: SurveyQuestion = {
  id: 'repetitive',
  question: 'Lặp lại?',
  multiple: true,
  options: [{ label: 'Không có', exclusive: true }, { label: 'Người nhận' }, { label: 'Hàng hóa' }]
};
const sections: SurveySection[] = [{ title: 'Nhóm', questions: [single, multi] }];

describe('toggleAnswer', () => {
  it('câu chọn 1: chọn phương án khác thì thay, bấm lại thì bỏ', () => {
    expect(toggleAnswer(single, [], 'Ổn')).toEqual(['Ổn']);
    expect(toggleAnswer(single, ['Ổn'], 'Khó')).toEqual(['Khó']);
    expect(toggleAnswer(single, ['Khó'], 'Khó')).toEqual([]);
  });

  it('câu chọn nhiều: giữ thứ tự phương án, phương án loại trừ bỏ các phương án khác và ngược lại', () => {
    expect(toggleAnswer(multi, ['Hàng hóa'], 'Người nhận')).toEqual(['Người nhận', 'Hàng hóa']);
    expect(toggleAnswer(multi, ['Người nhận', 'Hàng hóa'], 'Không có')).toEqual(['Không có']);
    expect(toggleAnswer(multi, ['Không có'], 'Hàng hóa')).toEqual(['Hàng hóa']);
  });
});

describe('surveyPayload', () => {
  it('chỉ gửi câu đã trả lời, kèm nội dung câu hỏi, cắt khoảng trắng ghi chú', () => {
    expect(isAnswered({ answers: [], note: '  ' })).toBe(false);
    expect(
      surveyPayload(sections, {
        repetitive: { answers: [], note: ' In nhãn ' },
        mobile: { answers: ['Ổn'], note: '' },
        khac: { answers: ['x'], note: '' }
      })
    ).toEqual([
      { id: 'mobile', question: 'Điện thoại?', answers: ['Ổn'], note: null },
      { id: 'repetitive', question: 'Lặp lại?', answers: [], note: 'In nhãn' }
    ]);
  });
});

describe('surveyStats', () => {
  it('đếm lượt chọn từng phương án và gom ghi chú', () => {
    const stats = surveyStats(sections, [
      { survey: [{ id: 'mobile', question: 'cũ', answers: ['Ổn'] }] },
      { survey: [{ id: 'mobile', question: 'Điện thoại?', answers: ['Khó'], note: 'Bảng bị tràn' }] },
      { survey: null },
      {}
    ]);
    expect(stats[0]).toEqual({
      id: 'mobile',
      question: 'Điện thoại?',
      responses: 2,
      options: [{ label: 'Ổn', count: 1 }, { label: 'Khó', count: 1 }],
      notes: ['Bảng bị tràn']
    });
    expect(stats[1]?.responses).toBe(0);
  });
});

describe('bộ câu hỏi', () => {
  it('mã câu hỏi không trùng, phương án trong câu không trùng, trong giới hạn backend', () => {
    const questions = SURVEY_SECTIONS.flatMap(s => s.questions);
    expect(new Set(questions.map(q => q.id)).size).toBe(questions.length);
    expect(questions.length).toBeLessThanOrEqual(30);
    for (const q of questions) {
      expect(q.id.length).toBeLessThanOrEqual(50);
      expect(q.question.length).toBeLessThanOrEqual(300);
      expect(q.options.length).toBeLessThanOrEqual(10);
      expect(new Set(q.options.map(o => o.label)).size).toBe(q.options.length);
    }
  });
});
