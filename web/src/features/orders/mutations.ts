import { useCallback } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ApiError } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { downloadBlob, fileNameFromDisposition } from '@/shared/lib/files';
import { useToast } from '@/shared/ui';
import { fetchOrdersExport, fetchPrintHtml } from './api';
import { PRINT_DOCUMENTS } from './constants';
import type { OrderFilters } from './types';

export type PrintDoc = (typeof PRINT_DOCUMENTS)[number]['key'];

const errorMessage = (e: unknown) => (e instanceof ApiError ? e.message : 'Có lỗi xảy ra, vui lòng thử lại');

/**
 * In chứng từ cho 1 hoặc nhiều đơn, 1 hoặc nhiều loại (nhiều loại → backend trả trang gộp, in lần lượt từng loại):
 * mở tab mới NGAY khi bấm (trình duyệt không chặn pop-up),
 * tải trang in từ backend rồi đổ vào tab — trang tự mở hộp thoại in.
 * `bills` có thể là hàm async (vd cấp mã bill rồi mới in); trả mảng rỗng = hủy, tự đóng tab (lỗi đã báo ở nơi gọi).
 */
export function usePrintDocuments() {
  const toast = useToast();
  const { t } = useI18n();
  return useCallback(
    async (bills: readonly string[] | (() => Promise<readonly string[]>), doc: PrintDoc | readonly PrintDoc[]) => {
      if (Array.isArray(bills) && bills.length === 0) return;
      const win = window.open('', '_blank');
      if (!win) {
        toast.show('Trình duyệt đang chặn cửa sổ in — hãy cho phép pop-up cho trang này', 'error');
        return;
      }
      const preparing = t('Đang chuẩn bị bản in…');
      win.document.title = preparing;
      win.document.body.innerHTML = `<p style="font:14px system-ui,sans-serif;padding:24px;color:#4d6456">${preparing}</p>`;
      try {
        const list = typeof bills === 'function' ? await bills() : bills;
        if (list.length === 0) {
          win.close();
          return;
        }
        const html = await fetchPrintHtml(list, typeof doc === 'string' ? doc : doc.join(','));
        win.document.open();
        win.document.write(html);
        win.document.close();
      } catch (e) {
        win.close();
        toast.show(errorMessage(e), 'error');
      }
    },
    [toast, t]
  );
}

/** Xuất bảng kê gửi hàng (.xlsx) theo bộ lọc đang áp dụng. */
export function useExportOrders() {
  const toast = useToast();
  return useMutation({
    mutationFn: async (filters: OrderFilters) => {
      const { blob, disposition } = await fetchOrdersExport(filters);
      downloadBlob(fileNameFromDisposition(disposition, 'bang-ke-gui-hang.xlsx'), blob);
    },
    onSuccess: () => toast.show('Đã xuất bảng kê gửi hàng', 'success'),
    onError: e => toast.show(errorMessage(e), 'error')
  });
}
