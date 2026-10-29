import { fill } from '@/shared/i18n';
import { IMPORT_LIMITS } from '../constants';
import type { ImportDefaults, ImportRow } from '../types';

/** Chỉ nhận .xlsx theo file mẫu (backend đọc bằng ClosedXML). */
export function checkImportFile(file: { name: string; size: number }): string | null {
  if (!/\.xlsx$/i.test(file.name)) return 'Chỉ nhận file Excel .xlsx — vui lòng dùng file mẫu.';
  if (file.size > IMPORT_LIMITS.maxMb * 1024 * 1024) return fill('File quá lớn (tối đa {mb} MB).', { mb: IMPORT_LIMITS.maxMb });
  return null;
}

/** Không bắt buộc chọn dịch vụ, nhưng đã chọn dịch vụ thì cần chọn hub. */
export function serviceHubError({ service, hub }: Pick<ImportDefaults, 'service' | 'hub'>): string | null {
  return service && !hub ? 'Đã chọn dịch vụ thì cần chọn Hub.' : null;
}

export function buildImportForm(file: File, defaults: ImportDefaults): FormData {
  const form = new FormData();
  form.append('file', file);
  if (defaults.service) {
    form.append('service', defaults.service);
    form.append('hub', defaults.hub);
  }
  if (defaults.branch) form.append('branch', defaults.branch);
  return form;
}

export type RowState = 'created' | 'error' | 'warning' | 'ok';

export function rowState(row: ImportRow): RowState {
  if (row.bill) return 'created';
  if (row.errors.length) return 'error';
  if (row.warnings.length) return 'warning';
  return 'ok';
}

/** Dòng lỗi lên đầu để khách sửa trước; còn lại giữ thứ tự trong file. */
export function sortRows(rows: readonly ImportRow[]): ImportRow[] {
  const rank: Record<RowState, number> = { error: 0, warning: 1, ok: 2, created: 2 };
  return [...rows].sort((a, b) => rank[rowState(a)] - rank[rowState(b)] || a.line - b.line);
}

export const formatKg = (kg: number) => `${Number(kg.toFixed(2))} kg`;
