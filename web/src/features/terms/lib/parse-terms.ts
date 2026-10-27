/** Một khoản: số khoản ("1.1") và nội dung. Dòng không có số đứng riêng thì `no` rỗng. */
export interface TermsClause {
  no: string;
  text: string;
}

export interface TermsSection {
  heading: string;
  clauses: TermsClause[];
}

export interface TermsDocument {
  title: string;
  /** Dòng giới thiệu công ty ngay dưới tiêu đề. */
  intro: string[];
  sections: TermsSection[];
  /** Dòng kết, vd "— HẾT — Bản ĐKDV-2026.11 · Viet An Express". */
  footer: string;
}

const SECTION = /^ĐIỀU\s+\d+\./;
const CLAUSE = /^(\d+\.\d+)\s+(.*)$/;
const FOOTER = /^—\s*HẾT\s*—/;

/**
 * Đọc văn bản điều khoản (file .txt): dòng 1 là tiêu đề, các dòng tới "ĐIỀU 1." là giới thiệu,
 * mỗi "ĐIỀU n. …" mở 1 mục, dòng "n.m …" là 1 khoản, dòng "— HẾT —" là dòng kết. Dòng trống bỏ qua.
 */
export function parseTerms(source: string): TermsDocument {
  const lines = source.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
  const doc: TermsDocument = { title: lines[0] ?? '', intro: [], sections: [], footer: '' };
  for (const line of lines.slice(1)) {
    if (FOOTER.test(line)) {
      doc.footer = line;
      continue;
    }
    if (SECTION.test(line)) {
      doc.sections.push({ heading: line, clauses: [] });
      continue;
    }
    const current = doc.sections.at(-1);
    if (!current) {
      doc.intro.push(line);
      continue;
    }
    const m = CLAUSE.exec(line);
    current.clauses.push(m ? { no: m[1]!, text: m[2]! } : { no: '', text: line });
  }
  return doc;
}

/** Mã phiên bản ghi ở dòng kết ("Bản ĐKDV-2026.11" → "ĐKDV-2026.11"); không có thì rỗng. */
export function termsVersion(doc: TermsDocument): string {
  return /Bản\s+(\S+)/.exec(doc.footer)?.[1] ?? '';
}
