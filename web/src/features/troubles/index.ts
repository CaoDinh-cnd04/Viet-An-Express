/** API công khai của feature — feature khác chỉ import từ đây. */
export { ReportTroubleDialog, type TroubleContext } from './components/ReportTroubleDialog';
export { useTroubles } from './api';
export { TroubleThread } from './components/TroubleThread';
export { TROUBLE_PRIORITY, TROUBLE_STATUS } from './constants';
export type { TroubleStatus, TroubleTicket } from './types';
