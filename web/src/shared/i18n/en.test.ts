import { describe, expect, it } from 'vitest';
import { findUntranslated, type Finding } from './coverage';
import { EN } from './en';
import { NO_TRANSLATE } from './no-translate';
import { RUNTIME_TEXTS } from './runtime-texts';
import { createTranslator } from './translate';

const translate = createTranslator(EN);
/** Có bản dịch: đúng khoá, khớp mẫu có biến, hoặc cố ý không dịch. */
const has = (text: string) => NO_TRANSLATE.has(text) || text in EN || translate('en', text) !== text;

// Toàn bộ mã giao diện (trừ test và chính từ điển).
const sources = import.meta.glob<string>(['/src/**/*.{ts,tsx}', '!/src/**/*.test.{ts,tsx}', '!/src/shared/i18n/**'], {
  query: '?raw',
  import: 'default',
  eager: true
});

// Mã backend (thông báo lỗi tiếng Việt trả về frontend, dữ liệu seed trong migration) — để biết khoá dịch còn được dùng.
const backendSources = import.meta.glob<string>(['../../../../backend/src/**/*.cs', '!../../../../backend/src/**/obj/**'], {
  query: '?raw',
  import: 'default',
  eager: true
});

describe('bản tiếng Anh', () => {
  it('mọi chữ tiếng Việt trong giao diện đều đã bọc t() và có bản dịch', () => {
    const problems: string[] = [];
    for (const [file, source] of Object.entries(sources)) {
      for (const f of findUntranslated(source, has, text => NO_TRANSLATE.has(text)) as Finding[]) problems.push(`${file} [${f.kind}] ${f.text}`);
    }
    expect(problems, `${problems.length} chỗ chưa dịch:\n${problems.join('\n')}`).toEqual([]);
  });

  it('khoá khai ở nhiều file từ điển phải cùng bản dịch', () => {
    const parts = import.meta.glob<Record<string, Record<string, string>>>(['./en/*.ts', '!./en/index.ts'], { eager: true });
    const seen = new Map<string, { file: string; value: string }>();
    const conflicts: string[] = [];
    for (const [file, mod] of Object.entries(parts)) {
      for (const dict of Object.values(mod)) {
        for (const [key, value] of Object.entries(dict)) {
          const prev = seen.get(key);
          if (prev && prev.value !== value) conflicts.push(`${key}: ${prev.file} "${prev.value}" ≠ ${file} "${value}"`);
          seen.set(key, { file, value });
        }
      }
    }
    expect(conflicts).toEqual([]);
  });

  it('bản dịch không để trống', () => {
    expect(Object.entries(EN).filter(([, v]) => !v.trim()).map(([k]) => k)).toEqual([]);
  });

  it('khoá nào cũng còn được dùng (mã web, thông báo / dữ liệu seed của backend, hoặc RUNTIME_TEXTS)', () => {
    const web = Object.values(sources).join('\n');
    const backend = Object.values(backendSources).join('\n');
    // Khoá có {biến}: so các đoạn chữ cố định (câu backend ghép số liệu vào, vd "Tối đa {n} ký tự").
    const fragmentsIn = (key: string, code: string) => {
      const parts = key.split(/\{\w+\}/).filter(p => p.trim().length >= 6);
      return parts.length > 0 && parts.every(p => code.includes(p));
    };
    // Mã nguồn viết xuống dòng dạng "\n" trong chuỗi — so cả dạng đó.
    const inCode = (key: string, code: string) => code.includes(key) || code.includes(key.replace(/\n/g, '\\n'));
    const unused = Object.keys(EN).filter(key =>
      !RUNTIME_TEXTS.has(key) && !inCode(key, web) && !inCode(key, backend)
      && !(key.includes('{') && (fragmentsIn(key, web) || fragmentsIn(key, backend))));
    expect(unused, `${unused.length} khoá không còn dùng — xoá khỏi en/*.ts (hoặc thêm vào runtime-texts.ts nếu là dữ liệu lúc chạy):\n${unused.join('\n')}`).toEqual([]);
  });
});
