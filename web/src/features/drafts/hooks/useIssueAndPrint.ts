import { usePrintDocuments } from '@/features/orders/mutations';
import { usePrintDraft } from '../api';

/** Chứng từ in ngay khi cấp bill — in lần lượt trong 1 hộp thoại. */
const ISSUE_PRINT_DOCS = ['bill-a4', 'invoice', 'label-a6'] as const;

/**
 * "In & cấp bill": backend cấp mã bill + khóa đơn, rồi mở luôn 1 bản in gộp: vận đơn A4, invoice, nhãn A6.
 * Tab in được mở ngay lúc bấm (tránh bị chặn pop-up); cấp mã lỗi thì tab tự đóng, lỗi báo qua toast của usePrintDraft.
 */
export function useIssueAndPrint() {
  const issue = usePrintDraft();
  const printDocuments = usePrintDocuments();

  /**
   * <paramref>getId</paramref>: id đơn có sẵn, hoặc hàm lưu đơn trước rồi trả id (Tạo đơn & in bill) — chạy sau khi đã mở tab in.
   * onDone: cấp bill xong; onFail: lưu / cấp bill lỗi (tab in tự đóng).
   */
  const run = (getId: string | (() => Promise<string>), onDone?: () => void, onFail?: () => void) =>
    void printDocuments(
      () =>
        (typeof getId === 'string' ? Promise.resolve(getId) : getId())
          .then(id => issue.mutateAsync(id))
          .then(res => {
            onDone?.();
            return res.billCode ? [res.billCode] : [];
          })
          .catch(() => {
            onFail?.();
            return [];
          }),
      ISSUE_PRINT_DOCS
    );

  return { run, isPending: issue.isPending, pendingId: issue.isPending ? issue.variables : undefined };
}
