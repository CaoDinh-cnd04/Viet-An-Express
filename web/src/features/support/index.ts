/** API công khai của feature — feature khác chỉ import từ đây. */
export { NewSupportRequestDialog } from './components/NewSupportRequestDialog';
export { SUPPORT_LIVE_EVENT, useSupportLive, useSupportTickets } from './api';
export { SupportThreadList } from './components/SupportThreadList';
export { SupportTicketDialog } from './components/SupportTicketDialog';
export { openTicketOf, ticketsOfBill } from './lib/by-bill';
export { kindOf, ticketSubline, ticketTitle } from './lib/ticket';
export { SENDER_INFO_TOPIC, SUPPORT_KINDS, SUPPORT_PRIORITY, SUPPORT_STATUS } from './constants';
export type { SupportKind, SupportStatus, SupportTicket } from './types';
// Bố cục + khung dùng chung với trang Hỗ trợ & góp ý của nhân viên Việt An (admin/ManagedSupport) — 2 giao diện đồng bộ.
export { FilterSelect, SupportFilterBar, SupportHeader, SupportShell, SupportSplit } from './components/SupportLayout';
export { SupportOverview, type StatusTile } from './components/SupportOverview';
export { StatusTag, SupportTicketList } from './components/SupportTicketList';
export { SupportFooterNote, SupportReplyBox, SupportTicketDetail, type DetailInfo } from './components/SupportTicketDetail';
export { periodStart, withinPeriod, lastActivity, type TicketPeriod } from './lib/requests';
export { TICKET_PAGE_SIZE, TICKET_PERIODS } from './constants';
