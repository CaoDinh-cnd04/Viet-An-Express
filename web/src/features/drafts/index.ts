/** API công khai của feature — feature khác chỉ import từ đây. */
export { DraftsTable } from './components/DraftsTable';
export { useIssueAndPrint } from './hooks/useIssueAndPrint';
export { useDrafts, useSaveDraft, useUpdateDraft, type Draft, type NewDraft } from './api';
