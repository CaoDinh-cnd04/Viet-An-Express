import { useMutation, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import { orderKeys } from '@/features/orders';
import { buildImportForm } from './lib/import-rows';
import type { ImportDefaults, ImportResult } from './types';

interface ImportResponse {
  message?: string;
  data: ImportResult;
}

interface ImportInput {
  file: File;
  defaults: ImportDefaults;
}

/** Kiểm tra file Excel: backend đọc, đối chiếu từng dòng với file mẫu — chưa tạo đơn. */
export function usePreviewImport() {
  return useMutation({
    mutationFn: ({ file, defaults }: ImportInput) => http.post<ImportResponse>('/orders/import/preview', buildImportForm(file, defaults))
  });
}

/** Tạo đơn cho các dòng hợp lệ (backend kiểm tra lại toàn bộ file) — đơn được cấp số vận đơn ngay. */
export function useCommitImport() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ file, defaults }: ImportInput) => http.post<ImportResponse>('/orders/import', buildImportForm(file, defaults)),
    onSuccess: () => void qc.invalidateQueries({ queryKey: orderKeys.all })
  });
}
