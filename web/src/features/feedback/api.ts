import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiUrl, getErrorMessage, http } from '@/shared/api/http';
import { useToast } from '@/shared/ui';
import type { FeedbackItem, SurveyAnswer } from './types';

const feedbackKey = ['account', 'feedback'] as const;

/** Góp ý đã gửi (tài khoản con chỉ thấy góp ý của mình). */
export function useMyFeedback() {
  return useQuery({
    queryKey: feedbackKey,
    queryFn: () => http.get<{ data: FeedbackItem[] }>('/account/feedback').then(r => r.data)
  });
}

/** Gửi góp ý: multipart (message, contact, rating, survey JSON, images[]). */
export function useSendFeedback() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (input: { message: string; contact: string; rating: number | null; survey: SurveyAnswer[]; images: File[] }) => {
      const form = new FormData();
      form.append('message', input.message);
      if (input.contact.trim()) form.append('contact', input.contact);
      if (input.rating) form.append('rating', String(input.rating));
      if (input.survey.length) form.append('survey', JSON.stringify(input.survey));
      input.images.forEach(f => form.append('images', f, f.name));
      return http.post<{ message: string; data: FeedbackItem }>('/account/feedback', form);
    },
    onSuccess: res => {
      toast.show(res.message, 'success');
      void qc.invalidateQueries({ queryKey: feedbackKey });
    },
    onError: e => toast.show(getErrorMessage(e, 'Chưa gửi được góp ý'), 'error')
  });
}

/** Ẩn góp ý khỏi trang khách; admin vẫn xem được nội dung và ảnh. */
export function useDeleteFeedback() {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: (id: string) => http.delete<{ message: string }>(`/account/feedback/${encodeURIComponent(id)}`),
    onSuccess: res => {
      toast.show(res.message, 'success');
      void qc.invalidateQueries({ queryKey: feedbackKey });
    },
    onError: e => toast.show(getErrorMessage(e, 'Chưa xóa được góp ý'), 'error')
  });
}

/** Đường dẫn ảnh góp ý (trình duyệt gửi kèm cookie phiên). */
export const feedbackImageUrl = (id: string) => apiUrl(`/account/feedback/images/${encodeURIComponent(id)}`);
