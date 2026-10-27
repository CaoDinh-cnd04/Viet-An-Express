/**
 * Lệnh in chuyển từ trang portal sang tab in qua localStorage.
 * Tab in mở bằng `noopener` (tiến trình riêng) nên hộp thoại in của tab đó không khóa trang portal,
 * khách mở thêm bản in khác mà không phải đóng tab cũ — đổi lại không ghi thẳng vào tab được, phải truyền qua storage.
 */

/** Đơn cần in + loại chứng từ (vd "bill-a4,invoice,label-a6"); `cancel` = hủy (cấp bill lỗi), tab in tự đóng. */
export type PrintJob = { bills: string[]; doc: string } | { cancel: true };

const PREFIX = 'va-print:';
/** Lệnh in còn nằm lại trong storage quá lâu (tab bị chặn pop-up, chưa kịp đọc) thì dọn. */
const TTL_MS = 10 * 60 * 1000;

export const printJobKey = (id: string) => PREFIX + id;

/** Đọc lệnh in đã lưu; dữ liệu hỏng / sai dạng → null. Hàm thuần — có test. */
export function parsePrintJob(raw: string | null): PrintJob | null {
  if (!raw) return null;
  try {
    const v = JSON.parse(raw) as { bills?: unknown; doc?: unknown; cancel?: unknown };
    if (v.cancel === true) return { cancel: true };
    if (Array.isArray(v.bills) && v.bills.every(b => typeof b === 'string') && typeof v.doc === 'string')
      return { bills: v.bills as string[], doc: v.doc };
  } catch {
    /* dữ liệu hỏng */
  }
  return null;
}

/** Các khóa lệnh in đã quá hạn. Hàm thuần — có test. */
export function expiredPrintJobKeys(entries: readonly [string, string | null][], now: number): string[] {
  return entries
    .filter(([key, raw]) => {
      if (!key.startsWith(PREFIX)) return false;
      try {
        const at = (JSON.parse(raw ?? '') as { at?: unknown }).at;
        return typeof at !== 'number' || now - at > TTL_MS;
      } catch {
        return true;
      }
    })
    .map(([key]) => key);
}

/** Ghi lệnh in cho tab `id` (kèm dọn lệnh cũ quá hạn). */
export function writePrintJob(id: string, job: PrintJob): void {
  try {
    const store = window.localStorage;
    const entries: [string, string | null][] = [];
    for (let i = 0; i < store.length; i++) {
      const key = store.key(i);
      if (key) entries.push([key, store.getItem(key)]);
    }
    expiredPrintJobKeys(entries, Date.now()).forEach(key => store.removeItem(key));
    store.setItem(printJobKey(id), JSON.stringify({ ...job, at: Date.now() }));
  } catch {
    /* storage bị chặn — tab in báo hết thời gian chờ */
  }
}
