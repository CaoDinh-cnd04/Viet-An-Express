import { useCallback } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ApiError } from '@/shared/api/http';
import { downloadBlob, fileNameFromDisposition } from '@/shared/lib/files';
import { uuid } from '@/shared/lib/uuid';
import { useToast } from '@/shared/ui';
import { fetchOrdersExport } from './api';
import { PRINT_DOCUMENTS } from './constants';
import { writePrintJob } from './lib/print-job';
import type { OrderFilters } from './types';

export type PrintDoc = (typeof PRINT_DOCUMENTS)[number]['key'];

const errorMessage = (e: unknown) => (e instanceof ApiError ? e.message : 'Có lỗi xảy ra, vui lòng thử lại');

/**
 * In chứng từ cho 1 hoặc nhiều đơn, 1 hoặc nhiều loại (nhiều loại → backend trả trang gộp, in lần lượt từng loại):
 * mở tab /print/:job NGAY khi bấm (trình duyệt không chặn pop-up), rồi chuyển lệnh in qua localStorage — tab tự tải và mở hộp thoại in.
 * Tab mở bằng noopener (tiến trình riêng): hộp thoại in đang mở không khóa trang portal, khách bấm in tiếp được ngay.
 * `bills` có thể là hàm async (vd cấp mã bill rồi mới in); trả mảng rỗng = hủy, tab tự đóng (lỗi đã báo ở nơi gọi).
 */
export function usePrintDocuments() {
  return useCallback(
    async (bills: readonly string[] | (() => Promise<readonly string[]>), doc: PrintDoc | readonly PrintDoc[]) => {
      if (Array.isArray(bills) && bills.length === 0) return;
      const id = uuid();
      const docs = typeof doc === 'string' ? doc : doc.join(',');
      if (Array.isArray(bills)) writePrintJob(id, { bills: [...bills], doc: docs });
      window.open(`${import.meta.env.BASE_URL}print/${id}`, '_blank', 'noopener');
      if (Array.isArray(bills)) return;
      const list = await (bills as () => Promise<readonly string[]>)().catch(() => [] as readonly string[]);
      writePrintJob(id, list.length === 0 ? { cancel: true } : { bills: [...list], doc: docs });
    },
    []
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
