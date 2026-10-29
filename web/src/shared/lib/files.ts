/** Tiện ích đọc / tạo file phía trình duyệt (CSV mẫu, import Excel dạng CSV). */

/** Tải một file văn bản về máy (VD file CSV mẫu). BOM giúp Excel đọc đúng tiếng Việt. */
export function downloadTextFile(filename: string, content: string, mime = 'text/csv;charset=utf-8'): void {
  downloadBlob(filename, new Blob(['﻿', content], { type: mime }));
}

/** Lưu blob thành file trên máy người dùng. */
export function downloadBlob(filename: string, blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const a = Object.assign(document.createElement('a'), { href: url, download: filename });
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

/**
 * Tên file trong header Content-Disposition: ưu tiên filename*=UTF-8''… (có dấu tiếng Việt), rồi filename=…
 * Không có thì trả `fallback`.
 */
export function fileNameFromDisposition(header: string | null | undefined, fallback: string): string {
  if (!header) return fallback;
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star?.[1]) {
    try {
      return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
    } catch {
      /* tên mã hoá hỏng → thử filename= */
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain?.[1]?.trim() || fallback;
}

export const readFileAsText = (file: File): Promise<string> => file.text();

/** Chuyển mảng dòng thành CSV (có escape dấu phẩy, ngoặc kép, xuống dòng). */
export function toCsv(rows: ReadonlyArray<ReadonlyArray<string | number>>): string {
  const cell = (v: string | number) => {
    const s = String(v);
    return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  return rows.map(r => r.map(cell).join(',')).join('\r\n');
}

/** Tách CSV đơn giản (hỗ trợ ô trong ngoặc kép). Dòng đầu là tiêu đề. */
export function parseCsv(text: string): { headers: string[]; rows: Record<string, string>[] } {
  const lines: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  const src = text.replace(/^\uFEFF/, '');

  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (quoted) {
      if (ch === '"' && src[i + 1] === '"') {
        cell += '"';
        i++;
      } else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ',' || ch === ';') {
      row.push(cell);
      cell = '';
    } else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && src[i + 1] === '\n') i++;
      row.push(cell);
      lines.push(row);
      row = [];
      cell = '';
    } else cell += ch;
  }
  if (cell || row.length) {
    row.push(cell);
    lines.push(row);
  }

  const nonEmpty = lines.filter(l => l.some(c => c.trim()));
  const headers = (nonEmpty[0] ?? []).map(h => h.trim());
  const rows = nonEmpty.slice(1).map(l => Object.fromEntries(headers.map((h, i) => [h, (l[i] ?? '').trim()])));
  return { headers, rows };
}
