import { stripDiacritics } from '@/shared/lib/text';
import { kindOf, ticketTitle, TROUBLE_KINDS, type TroubleStatus, type TroubleTicket } from '@/features/troubles';
import type { AdminTrouble } from '../api';

export type TroubleFilter = 'open' | TroubleStatus | 'all';

export const TROUBLE_FILTERS: ReadonlyArray<{ key: TroubleFilter; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'new', label: 'Mới' },
  { key: 'doing', label: 'Đang xử lý' },
  { key: 'waitc', label: 'Chờ khách' },
  { key: 'done', label: 'Đã xử lý' },
  { key: 'all', label: 'Tất cả' }
];

export const matchesFilter = (x: AdminTrouble, f: TroubleFilter) =>
  f === 'all' ? true : f === 'open' ? x.ticket.status !== 'done' : x.ticket.status === f;

const norm = (s: string) => stripDiacritics(s).toLowerCase();

/** Lọc theo trạng thái + tìm (mã, vận đơn, khách, người gửi, loại, nội dung) — không phân biệt dấu / hoa thường. */
export function filterTroubles(items: readonly AdminTrouble[], filter: TroubleFilter, query: string): AdminTrouble[] {
  const q = norm(query.trim());
  return items.filter(x => {
    if (!matchesFilter(x, filter)) return false;
    if (!q) return true;
    const t = x.ticket;
    return norm([t.id, t.bill, t.cnee, t.type, t.subject ?? '', t.desc, t.req, x.customerCode, x.companyName, adminKindLabel(t), ticketTitle(t)].join(' ')).includes(q);
  });
}

/** Lọc nhân viên phụ trách: '' = tất cả, 'none' = khách chưa phân công, còn lại = USERS.ID. */
export const UNASSIGNED = 'none';

export const matchesStaff = (x: AdminTrouble, staff: string) =>
  !staff || (staff === UNASSIGNED ? !x.managerId : String(x.managerId ?? '') === staff);

/** Nhân viên có trong danh sách (không trùng, theo tên) — cho ô lọc nhân viên. */
export function staffOptions(items: readonly AdminTrouble[]): Array<{ id: string; name: string }> {
  const seen = new Map<string, string>();
  for (const x of items) if (x.managerId) seen.set(String(x.managerId), x.managerName?.trim() || `#${x.managerId}`);
  return [...seen].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

/** Lời nhắn khách vãng lai gửi ở trang chủ (POST /public/contact, TContact "Portal - Liên hệ") — không thuộc 3 loại khách chọn ở /troubles. */
export const isContactMessage = (t: TroubleTicket) => (t.kind as string | undefined) === 'lien_he';

/** Tên loại ở trang quản trị: 3 loại của khách + "Liên hệ trang chủ". */
export const adminKindLabel = (t: TroubleTicket) => (isContactMessage(t) ? 'Liên hệ trang chủ' : TROUBLE_KINDS[kindOf(t)].label);

export interface TroubleAction {
  status: TroubleStatus;
  label: string;
  primary?: boolean;
}

/**
 * Nút ở chân khung chi tiết: mỗi nút đổi sang 1 trạng thái (kèm ghi chú nếu có) — 1 lần bấm.
 * `canWait` = false (lời nhắn trang chủ — khách chưa có tài khoản) thì bỏ nút "Chờ khách".
 */
export function troubleActions(status: TroubleStatus, canWait = true): TroubleAction[] {
  return actionsOf(status).filter(a => canWait || a.status !== 'waitc');
}

function actionsOf(status: TroubleStatus): TroubleAction[] {
  switch (status) {
    case 'new':
      return [{ status: 'waitc', label: 'Chờ khách' }, { status: 'done', label: 'Đã xử lý' }, { status: 'doing', label: 'Tiếp nhận', primary: true }];
    case 'doing':
      return [{ status: 'waitc', label: 'Chờ khách' }, { status: 'done', label: 'Đã xử lý', primary: true }];
    case 'waitc':
      return [{ status: 'doing', label: 'Đang xử lý' }, { status: 'done', label: 'Đã xử lý', primary: true }];
    case 'done':
      return [{ status: 'doing', label: 'Mở lại', primary: true }];
  }
}
