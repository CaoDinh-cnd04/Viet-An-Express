import { stripDiacritics } from '@/shared/lib/text';
import { kindOf, ticketTitle, SUPPORT_KINDS, type StatusTile, type SupportStatus, type SupportTicket } from '@/features/support';
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

/** Lọc theo khách (mã khách TCustomer) — trống = mọi khách; so không phân biệt hoa thường, bỏ khoảng trắng. */
export const matchesCustomer = (x: AdminSupportTicket, customerCode: string) =>
  !customerCode || x.customerCode.trim().toLowerCase() === customerCode.trim().toLowerCase();

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
    // Không còn nút "Chờ khách" (06/11/2026): khách nhắn lúc nào cũng được, không cần CS cho phép.
    case 'new':
      return [{ status: 'done', label: 'Đã xử lý' }, { status: 'doing', label: 'Tiếp nhận', primary: true }];
    case 'doing':
      return [{ status: 'done', label: 'Đã xử lý', primary: true }];
    case 'waitc': // dòng cũ CS đã chuyển "Chờ khách"
      return [{ status: 'doing', label: 'Đang xử lý' }, { status: 'done', label: 'Đã xử lý', primary: true }];
    case 'done':
      return [{ status: 'doing', label: 'Mở lại', primary: true }];
  }
}

// ---------- Trang Hỗ trợ & góp ý của nhân viên Việt An (ManagedSupport) — cùng bố cục trang khách ----------

/** Ô trạng thái đầu trang CS. */
export type ManagedFilter = 'new' | 'doing' | 'waitc' | 'done' | 'all';

export const MANAGED_TILES: ReadonlyArray<StatusTile<ManagedFilter>> = [
  { key: 'new', label: 'Mới', tone: 'var(--color-info)', icon: 'bell' },
  { key: 'doing', label: 'Đang xử lý', tone: 'var(--color-status-fly)', icon: 'clock' },
  { key: 'waitc', label: 'Chờ khách', tone: 'var(--color-warning)', icon: 'message' },
  { key: 'done', label: 'Đã xử lý', tone: 'var(--color-success)', icon: 'checkCircle' },
  { key: 'all', label: 'Tất cả', tone: 'var(--color-text-muted)', icon: 'inbox' }
];

/** Ô mở đầu: còn yêu cầu mới → Mới; rồi Đang xử lý, Chờ khách; không còn gì mở → Tất cả. */
export function defaultManagedFilter(items: readonly AdminSupportTicket[]): ManagedFilter {
  for (const f of ['new', 'doing', 'waitc'] as const) if (items.some(x => x.ticket.status === f)) return f;
  return 'all';
}

export function managedCounts(items: readonly AdminSupportTicket[]): Record<ManagedFilter, number> {
  const n = (s: SupportStatus) => items.filter(x => x.ticket.status === s).length;
  return { new: n('new'), doing: n('doing'), waitc: n('waitc'), done: n('done'), all: items.length };
}

/** Lọc loại ở trang CS: hỗ trợ (gồm sự cố cũ) / góp ý / lời nhắn trang chủ; trống = tất cả. */
export type ManagedKind = '' | 'ho_tro' | 'gop_y' | 'lien_he';

export const MANAGED_KINDS: ReadonlyArray<{ value: ManagedKind; label: string }> = [
  { value: '', label: 'Tất cả' },
  { value: 'ho_tro', label: 'Hỗ trợ & sự cố' },
  { value: 'gop_y', label: 'Góp ý' },
  { value: 'lien_he', label: 'Liên hệ trang chủ' }
];

export function matchesKind(x: AdminSupportTicket, kind: ManagedKind): boolean {
  if (!kind) return true;
  if (isContactMessage(x.ticket)) return kind === 'lien_he';
  return kind === 'gop_y' ? kindOf(x.ticket) === 'gop_y' : kind === 'ho_tro' && kindOf(x.ticket) !== 'gop_y';
}

/** Câu trả lời mẫu cho CS — chọn để điền sẵn ô trả lời rồi sửa thêm. Sửa / thêm câu tại đây. */
export const CS_REPLY_TEMPLATES: readonly string[] = [
  'Dạ em đã nhận yêu cầu, em kiểm tra với hãng và báo lại anh/chị trong hôm nay ạ.',
  'Anh/chị gửi giúp em số điện thoại và địa chỉ đầy đủ của người nhận để em bổ sung cho hãng ạ.',
  'Anh/chị gửi giúp em ảnh kiện hàng và ảnh bill lúc giao ạ.',
  'Đơn đã giao thành công, anh/chị xem hành trình ở trang Tra cứu ạ.',
  'Em đã cập nhật thông tin theo yêu cầu, anh/chị kiểm tra lại giúp em ạ.'
];
