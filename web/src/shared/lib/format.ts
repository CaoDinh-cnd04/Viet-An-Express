/** Định dạng số / tiền / ngày dùng chung — mọi nơi hiển thị số phải qua đây để đồng nhất. */

const numberFmt = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });
const vndFmt = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

export const formatNumber = (n: number): string => numberFmt.format(n);

export const formatVnd = (n: number): string => `${vndFmt.format(Math.round(n))} đ`;

export const formatPercent = (ratio: number): string => `${numberFmt.format(ratio * 100)}%`;

/** "59,9 USD" / "59,9" — trống khi chưa có giá trị. */
export const formatMoney = (value: number | null | undefined, currency?: string | null): string =>
  value === null || value === undefined ? '' : `${numberFmt.format(value)}${currency ? ` ${currency}` : ''}`;

const pad2 = (n: number): string => String(n).padStart(2, '0');

/** Date → 'dd/mm/yyyy' (giờ máy — cùng dạng ngày backend trả). */
export const formatViDate = (d: Date): string => `${pad2(d.getDate())}/${pad2(d.getMonth() + 1)}/${d.getFullYear()}`;

/** Ngày 'dd/mm/yyyy' → "Hôm nay" / "Hôm qua" / giữ nguyên (câu tiếng Việt, nơi hiển thị bọc t()). */
export function relativeDayLabel(day: string, today: Date): string {
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
  return day === formatViDate(today) ? 'Hôm nay' : day === formatViDate(yesterday) ? 'Hôm qua' : day;
}

/** 'yyyy-mm-dd' → 'dd/mm/yyyy'; chuỗi khác giữ nguyên. */
export function formatIsoDate(value: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : value;
}

/** '2026-10-06T08:05:00' (giờ Việt Nam từ backend) → '06/10/2026 08:05'; giá trị trống / sai dạng → ''. */
export function formatIsoDateTime(value?: string | null): string {
  const m = value ? /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/.exec(value) : null;
  return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : '';
}

/** Ngày hôm nay dạng 'yyyy-mm-dd' (giá trị của input[type=date]). */
export function todayIso(offsetDays = 0): string {
  const d = new Date(Date.now() + offsetDays * 86_400_000);
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}
