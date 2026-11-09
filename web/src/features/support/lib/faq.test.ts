import { describe, expect, it } from 'vitest';
import { FAQ, TOPIC_FAQ } from '../constants';
import { faqForTopic, searchFaq, type FaqItem } from './faq';

const items: FaqItem[] = [
  { cat: 'don', q: 'Khi nào được cấp mã bill?', a: 'Bấm In & cấp bill.' },
  { cat: 'cuoc', q: 'Cước tính thế nào?', a: 'Lấy cân lớn hơn.' },
  { cat: 'cuoc', q: 'Phụ phí quá khổ?', a: 'Kiện vượt giới hạn của hãng.' },
  { cat: 'cuoc', q: 'Phụ phí vùng xa?', a: 'Khu vực ở xa.' }
];

describe('câu hỏi thường gặp', () => {
  it('lọc nhóm + tìm không dấu trong câu hỏi lẫn câu trả lời', () => {
    expect(searchFaq(items, '', '')).toHaveLength(4);
    expect(searchFaq(items, 'cuoc', '').map(f => f.q)).toHaveLength(3);
    expect(searchFaq(items, '', 'cuoc').map(f => f.q)).toEqual(['Cước tính thế nào?']);
    expect(searchFaq(items, '', 'GIOI HAN').map(f => f.q)).toEqual(['Phụ phí quá khổ?']);
    expect(searchFaq(items, 'don', 'phu phi')).toEqual([]);
  });

  it('gợi ý theo vấn đề: tối đa 2 câu cùng nhóm, vấn đề không gắn nhóm thì không gợi ý', () => {
    expect(faqForTopic(items, { 'Cước & phụ phí': 'cuoc' }, 'Cước & phụ phí').map(f => f.q)).toEqual(['Cước tính thế nào?', 'Phụ phí quá khổ?']);
    expect(faqForTopic(items, { 'Cước & phụ phí': 'cuoc' }, 'Khác')).toEqual([]);
  });

  it('mỗi nhóm vấn đề gợi ý đều có câu hỏi', () => {
    for (const cat of new Set(Object.values(TOPIC_FAQ))) expect(FAQ.some(f => f.cat === cat)).toBe(true);
  });
});
