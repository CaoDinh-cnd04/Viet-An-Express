import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import { toServiceCatalog, type ServiceCatalog } from '@/shared/config/domain';

/** 1 hãng / dịch vụ trong danh mục hệ thống cũ — GET /services/hubs (API_CONTRACT.md §3). */
export interface ServiceCarrier {
  carrier: string;
  hubs: Array<{ name: string; code?: string | null }>;
}

const TEN_MINUTES = 10 * 60 * 1000;

/**
 * Danh mục hãng → hub đọc từ bảng hệ thống cũ (dbo.TGroupBaseCode / dbo.TBaseCode) — tên trùng đúng chữ MaVanDon.Dich_Vu
 * hệ thống cũ / CMS khớp. Đang tải, lỗi, hoặc backend chưa đọc được bảng → danh mục dự phòng (shared/config/domain.ts).
 */
export function useServiceCatalog(): ServiceCatalog {
  const query = useQuery({
    queryKey: ['services', 'hubs'],
    queryFn: () => http.get<{ data: ServiceCarrier[] }>('/services/hubs').then(r => r.data),
    staleTime: TEN_MINUTES,
    gcTime: TEN_MINUTES,
    retry: 1
  });
  return useMemo(() => toServiceCatalog(query.data), [query.data]);
}
