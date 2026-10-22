import { useCallback } from 'react';
import { useToast } from '@/shared/ui';
import { copyText } from './clipboard';

/** Sao chép chữ vào clipboard và báo toast. */
export function useCopyToClipboard() {
  const toast = useToast();
  return useCallback(
    async (text: string, message = 'Đã sao chép') => {
      try {
        await copyText(text);
        toast.show(message, 'success');
      } catch {
        toast.show('Trình duyệt không cho phép sao chép', 'error');
      }
    },
    [toast]
  );
}
