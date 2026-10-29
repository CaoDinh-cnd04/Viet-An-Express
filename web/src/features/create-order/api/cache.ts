import { type QueryClient } from '@tanstack/react-query';

// Hằng số / hàm phụ dùng chung giữa các nhóm API (thời gian giữ cache, đổi sao yêu thích ngay trên giao diện).

/** Đổi sao trong cache ngay khi bấm; trả dữ liệu cũ để hoàn lại nếu máy chủ báo lỗi. */
export async function toggleCached<T extends { id?: string; isFavorite?: boolean }>(qc: QueryClient, key: readonly string[], id: string, isFavorite: boolean) {
  await qc.cancelQueries({ queryKey: key });
  const previous = qc.getQueryData<T[]>(key);
  qc.setQueryData<T[]>(key, list => list?.map(x => (x.id === id ? { ...x, isFavorite } : x)));
  return previous;
}

export const ONE_HOUR = 60 * 60 * 1000;
