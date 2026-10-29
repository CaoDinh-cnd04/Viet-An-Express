import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import { toggleCached, ONE_HOUR } from './cache';

// Nhóm hàng hóa (dbo.NhomHangHoa) — GET/POST/PUT/DELETE /catalog/categories.

/** Nhóm hàng hóa (dbo.NhomHangHoa). */
export interface Category {
  id: string;
  name: string;
  isFavorite: boolean;
  /** Nhóm khách tự tạo — sửa / xóa được; false = nhóm chung Việt An. */
  isOwn: boolean;
  suggestions: Array<{ en: string; vi: string; hs: string }>;
}

export interface CategoryInput {
  name: string;
  isFavorite: boolean;
}

const CATEGORIES_KEY = ['catalog', 'categories'] as const;

export function useCategories() {
  return useQuery({
    queryKey: CATEGORIES_KEY,
    queryFn: () => http.get<{ data: Category[] }>('/catalog/categories').then(r => r.data),
    staleTime: ONE_HOUR
  });
}

/** Thêm (không có id) hoặc sửa nhóm hàng của khách. */
export function useSaveCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: CategoryInput & { id?: string }) =>
      id
        ? http.put<{ data: Category; message: string }>(`/catalog/categories/${encodeURIComponent(id)}`, body)
        : http.post<{ data: Category; message: string }>('/catalog/categories', body),
    onSuccess: () => void qc.invalidateQueries({ queryKey: CATEGORIES_KEY })
  });
}

export function useDeleteCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => http.delete<{ message: string }>(`/catalog/categories/${encodeURIComponent(id)}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: CATEGORIES_KEY })
  });
}

/** Đánh dấu / bỏ yêu thích nhóm hàng — được cả nhóm chung Việt An (backend lưu riêng cho từng khách). */
export function useFavoriteCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isFavorite }: { id: string; isFavorite: boolean }) =>
      http.put<{ message: string }>(`/catalog/categories/${encodeURIComponent(id)}/favorite`, { isFavorite }),
    // Sao đổi ngay trên giao diện, lỗi thì trả lại như cũ
    onMutate: ({ id, isFavorite }) => toggleCached<Category>(qc, CATEGORIES_KEY, id, isFavorite),
    onError: (_e, _v, previous) => qc.setQueryData(CATEGORIES_KEY, previous),
    onSettled: () => void qc.invalidateQueries({ queryKey: CATEGORIES_KEY })
  });
}
