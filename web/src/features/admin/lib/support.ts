import { stripDiacritics } from '@/shared/lib/text';
import { kindOf, ticketTitle, SUPPORT_KINDS, type SupportStatus, type SupportTicket } from '@/features/support';
import type { AdminSupportTicket } from '../api';

export type SupportFilter = 'open' | SupportStatus | 'all';

export const SUPPORT_FILTERS: ReadonlyArray<{ key: SupportFilter; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'new', label: 'Mới' },
  { key: 'doing', label: 'Đang xử lý' },
  { key: 'waitc', label: 'Chờ khách' },
  { key: 'done', label: 'Đã xử lý' },
  { key: 'all', label: 'Tất cả' }
];

export const matchesFilter = (x: AdminSupportTicket, f: SupportFilter) =>
  f === 'all' ? true : f === 'open' ? x.ticket.status !== 'done' : x.ticket.status === f;

const norm = (s: string) => stripDiacritics(s).toLowerCase();

/** Lọc theo trạng thái + tìm (mã, vận đơn, khách, người gửi, loại, nội dung) — không phân biệt dấu / hoa thường. */
export function filterSupportTickets(items: readonly AdminSupportTicket[], filter: SupportFilter, query: string): AdminSupportTicket[] {
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

export const matchesStaff = (x: AdminSupportTicket, staff: string) =>
  !staff || (staff === UNASSIGNED ? !x.managerId : String(x.managerId ?? '') === staff);

/** Nhân viên có trong danh sách (không trùng, theo tên) — cho ô lọc nhân viên. */
export function staffOptions(items: readonly AdminSupportTicket[]): Array<{ id: string; name: string }> {
  const seen = new Map<string, string>();
  for (const x of items) if (x.managerId) seen.set(String(x.managerId), x.managerName?.trim() || `#${x.managerId}`);
  return [...seen].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

/** Lời nhắn khách vãng lai gửi ở trang chủ (POST /public/contact, TContact "Portal - Liên hệ") — không thuộc 3 loại khách chọn ở /troubles. */
export const isContactMessage = (t: SupportTicket) => (t.kind as string | undefined) === 'lien_he';

/** Tên loại ở trang quản trị: 3 loại của khách + "Liên hệ trang chủ". */
export const adminKindLabel = (t: SupportTicket) => (isContactMessage(t) ? 'Liên hệ trang chủ' : SUPPORT_KINDS[kindOf(t)].label);

export interface TicketAction {
  status: SupportStatus;
  label: string;
  primary?: boolean;
}

/**
 * Nút ở chân khung chi tiết: mỗi nút đổi sang 1 trạng thái (kèm ghi chú nếu có) — 1 lần bấm.
 * `canWait` = false (lời nhắn trang chủ — khách chưa có tài khoản) thì bỏ nút "Chờ khách".
 */
export function ticketActions(status: SupportStatus, canWait = true): TicketAction[] {
  return actionsOf(status).filter(a => canWait || a.status !== 'waitc');
}

function actionsOf(status: SupportStatus): TicketAction[] {
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
