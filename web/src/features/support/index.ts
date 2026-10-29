/** API công khai của feature — feature khác chỉ import từ đây. */
export { NewSupportRequestDialog } from './components/NewSupportRequestDialog';
export { useSupportTickets } from './api';
export { SupportThreadList } from './components/SupportThreadList';
export { SupportTicketDialog } from './components/SupportTicketDialog';
export { openTicketOf, ticketsOfBill } from './lib/by-bill';
export { kindOf, ticketSubline, ticketTitle } from './lib/ticket';
export { SENDER_INFO_TOPIC, SUPPORT_KINDS, SUPPORT_PRIORITY, SUPPORT_STATUS } from './constants';
export type { SupportKind, SupportStatus, SupportTicket } from './types';
