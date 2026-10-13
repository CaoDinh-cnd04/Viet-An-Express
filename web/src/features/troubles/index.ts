/** API công khai của feature — feature khác chỉ import từ đây. */
export { ReportTroubleDialog, type TroubleContext } from './components/ReportTroubleDialog';
export { useTroubles } from './api';
export { TroubleDetailDialog } from './components/TroubleDetailDialog';
export { TroubleThread } from './components/TroubleThread';
export { openTroubleOf, troublesOfBill } from './lib/by-bill';
export { TROUBLE_PRIORITY, TROUBLE_STATUS } from './constants';
export type { TroubleStatus, TroubleTicket } from './types';
