import { describe, expect, it } from 'vitest';
import source from '../content/dieu-khoan-dich-vu.txt?raw';
import { parseTerms, termsVersion } from './parse-terms';

describe('parseTerms', () => {
  it('tách tiêu đề, giới thiệu, mục, khoản và dòng kết', () => {
    const doc = parseTerms(`ĐIỀU KHOẢN DỊCH VỤ
Công ty A

ĐIỀU 1. HIỆU LỰC
1.1 Nội dung một.
1.2 Nội dung hai.
Dòng không số.
ĐIỀU 2. KHÁC
2.1 Nội dung ba.
— HẾT — Bản ĐKDV-2026.11 · Viet An Express`);
    expect(doc.title).toBe('ĐIỀU KHOẢN DỊCH VỤ');
    expect(doc.intro).toEqual(['Công ty A']);
    expect(doc.sections.map(s => s.heading)).toEqual(['ĐIỀU 1. HIỆU LỰC', 'ĐIỀU 2. KHÁC']);
    expect(doc.sections[0]!.clauses).toEqual([
      { no: '1.1', text: 'Nội dung một.' },
      { no: '1.2', text: 'Nội dung hai.' },
      { no: '', text: 'Dòng không số.' }
    ]);
    expect(termsVersion(doc)).toBe('ĐKDV-2026.11');
  });

  it('đọc đủ 17 điều của bản điều khoản đang dùng', () => {
    const doc = parseTerms(source);
    expect(doc.sections).toHaveLength(17);
    expect(doc.sections[16]!.heading).toBe('ĐIỀU 17. DỊCH VỤ VẬN TẢI QUỐC TẾ');
    expect(doc.sections[16]!.clauses.at(-1)!.no).toBe('17.7');
    expect(doc.sections.every(s => s.clauses.length > 0 && s.clauses.every(c => c.no))).toBe(true);
    expect(termsVersion(doc)).toBe('ĐKDV-2026.11');
  });
});
