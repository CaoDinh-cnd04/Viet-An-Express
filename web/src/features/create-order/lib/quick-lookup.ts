import { stripDiacritics } from '@/shared/lib/text';
import type { Country } from './geo';

/** 1 kết quả tra HS code: mã HS + tên hàng tiếng Anh / Việt + nguồn (nhóm hàng Việt An hoặc thư viện mặt hàng của khách). */
export interface HsHit {
  hs: string;
  en: string;
  vi: string;
  source: string;
}

const norm = (s: string | null | undefined) => stripDiacritics(s ?? '').toLowerCase().trim();
const digits = (s: string) => s.replace(/\D/g, '');

/**
 * Tìm HS code theo tên hàng (Anh / Việt, không dấu) hoặc theo số HS (bỏ dấu chấm, khớp đầu mã).
 * Bỏ trùng theo HS + tên tiếng Anh; số HS khớp đầu mã xếp trước.
 */
export function searchHs(query: string, sources: ReadonlyArray<{ source: string; items: ReadonlyArray<{ hs: string; en: string; vi: string }> }>, limit = 8): HsHit[] {
  const q = norm(query);
  if (q.length < 2) return [];
  const qDigits = digits(q);
  const isCode = qDigits.length >= 2 && qDigits.length === q.replace(/[\s.]/g, '').length;
  const seen = new Set<string>();
  const hits: Array<HsHit & { rank: number }> = [];
  for (const { source, items } of sources) {
    for (const it of items) {
      if (!it.hs?.trim()) continue;
      const key = `${digits(it.hs)}|${norm(it.en)}`;
      if (seen.has(key)) continue;
      const byCode = isCode && digits(it.hs).startsWith(qDigits);
      const byName = !isCode && (norm(it.en).includes(q) || norm(it.vi).includes(q));
      if (!byCode && !byName) continue;
      seen.add(key);
      hits.push({ hs: it.hs.trim(), en: it.en.trim(), vi: it.vi.trim(), source, rank: byCode ? 0 : norm(it.en).startsWith(q) || norm(it.vi).startsWith(q) ? 1 : 2 });
    }
  }
  return hits
    .sort((a, b) => a.rank - b.rank)
    .slice(0, limit)
    .map(({ rank: _rank, ...h }) => h);
}

/** Tìm nước theo tên (không dấu), mã 2 chữ hoặc mã điện thoại ("+84", "84"). Khớp mã đúng xếp trước. */
export function searchCountries(query: string, countries: ReadonlyArray<Country>, limit = 8): Country[] {
  const q = norm(query);
  if (!q) return [];
  const dial = digits(q);
  const rank = (c: Country) => {
    if (norm(c.code) === q) return 0;
    if (dial && digits(c.dialCode ?? '') === dial) return 1;
    if (norm(c.name).startsWith(q)) return 2;
    if (norm(c.name).includes(q)) return 3;
    if (dial && digits(c.dialCode ?? '').startsWith(dial)) return 4;
    return -1;
  };
  return countries
    .map(c => ({ c, r: rank(c) }))
    .filter(x => x.r >= 0)
    .sort((a, b) => a.r - b.r || a.c.name.localeCompare(b.c.name))
    .slice(0, limit)
    .map(x => x.c);
}
