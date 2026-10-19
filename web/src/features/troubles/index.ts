/** API công khai của feature — feature khác chỉ import từ đây. */
export { ReportTroubleDialog, type TroubleContext, type TroublePreset } from './components/ReportTroubleDialog';
export { useTroubles } from './api';
export { TroubleDetailDialog } from './components/TroubleDetailDialog';
export { openTroubleOf, troublesOfBill } from './lib/by-bill';
export { kindOf, ticketSubline, ticketTitle } from './lib/chat';
export { SENDER_INFO_TOPIC, TROUBLE_KINDS, TROUBLE_PRIORITY, TROUBLE_STATUS } from './constants';
export type { TroubleKind, TroubleStatus, TroubleTicket } from './types';
