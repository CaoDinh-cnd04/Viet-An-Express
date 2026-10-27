/**
 * Định dạng kết quả tra cứu theo mẫu trang mytracking.top cũ: ngày yyyy-MM-dd, hành trình gom theo ngày, nơi gửi / đến "A - B".
 * Hàm thuần — có test.
 */

export interface ClassicEvent { time: string; title: string; location?: string }
export interface ClassicDay { date: string; items: { time: string; title: string; location: string }[] }

const VN = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\s+(\d{1,2}:\d{2}))?/;
const ISO = /^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}:\d{2}))?/;

/** "10/10/2026 11:25" hoặc "2026-10-10T11:25" → { date: "2026-10-10", time: "11:25" }; không đọc được → nguyên văn. */
export function splitClassic(value: string | undefined): { date: string; time: string } {
  const text = (value ?? '').trim();
  const vn = VN.exec(text);
  if (vn) return { date: `${vn[3]}-${vn[2]!.padStart(2, '0')}-${vn[1]!.padStart(2, '0')}`, time: vn[4] ?? '' };
  const iso = ISO.exec(text);
  if (iso) return { date: `${iso[1]}-${iso[2]}-${iso[3]}`, time: iso[4] ?? '' };
  return { date: text, time: '' };
}

/** Ngày "dd/MM/yyyy" → "yyyy-MM-dd"; trống → ''. */
export const classicDate = (value: string | undefined): string => splitClassic(value).date;

/** "HCMC, Vietnam" → "HCMC - Vietnam". */
export const classicPlace = (value: string | undefined): string => (value ?? '').split(',').map(p => p.trim()).filter(Boolean).join(' - ');

/** Hành trình (mới nhất trước) gom theo ngày, giữ thứ tự. */
export function groupClassic(events: readonly ClassicEvent[]): ClassicDay[] {
  const days: ClassicDay[] = [];
  for (const e of events) {
    const { date, time } = splitClassic(e.time);
    const line = { time, title: e.title, location: e.location ?? '' };
    const last = days[days.length - 1];
    if (last && last.date === date) last.items.push(line);
    else days.push({ date, items: [line] });
  }
  return days;
}

/** Dòng trạng thái: "Information submitted to local courier (2026-10-10 11:25)". */
export function classicStatus(latest: ClassicEvent | undefined, fallback: string): string {
  if (!latest) return fallback;
  const { date, time } = splitClassic(latest.time);
  const when = [date, time].filter(Boolean).join(' ');
  return when ? `${latest.title} (${when})` : latest.title;
}
