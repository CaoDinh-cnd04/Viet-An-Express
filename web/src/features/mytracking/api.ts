import { useQuery } from '@tanstack/react-query';
import type { TrackResult } from '@/features/landing';
import { http } from '@/shared/api/http';
import { parseConfig, type MyTrackingConfig } from './schema';

/** Thông tin trang MyTracking trên server: mã khách để dựng link (lib/link.ts). Có dòng cấu hình là trang hiện công khai. */
export interface Publication {
  /** Mã khách trong link /tracking/{mã khách}/{số bill}. */
  customerCode?: string;
}

/** GET / PUT /account/mytracking — `config` null khi khách chưa cấu hình. */
export interface MyTrackingResponse extends Publication {
  config: unknown;
  /** Tên công ty theo hồ sơ khách (TCustomer.InvoiceCustomerName, trống thì CustomerName) — trang MyTracking luôn dùng tên này, khách không sửa. */
  companyName?: string;
}

export interface SaveMyTrackingRequest {
  config: MyTrackingConfig;
}

/** Transport cho ApiConfigRepository — chỉ tài khoản chính (admin) gọi được. */
export const myTrackingApi = {
  load: () => http.get<{ data: MyTrackingResponse }>('/account/mytracking').then(r => r.data),
  save: (body: SaveMyTrackingRequest) => http.put<{ data: MyTrackingResponse }>('/account/mytracking', body).then(r => r.data)
};

/** Trang MyTracking theo mã khách, không cần đăng nhập — GET /public/mytracking/customers/:code. 404 = khách chưa cấu hình / không có. */
export function usePublicMyTracking(customerCode: string) {
  return useQuery({
    queryKey: ['public-mytracking', customerCode],
    queryFn: () =>
      http.get<{ data: { config: unknown; customerCode: string } }>(`/public/mytracking/customers/${encodeURIComponent(customerCode)}`)
        .then(r => ({ customerCode: r.data.customerCode, config: parseConfig(r.data.config) })),
    staleTime: 5 * 60_000,
    retry: false
  });
}

/** Tra cứu chỉ trong đơn của khách (trang /tracking/{mã khách}/...) — POST /public/tracking/customers/:code. */
export function useCustomerTracking(customerCode: string, bills: readonly string[]) {
  return useQuery({
    queryKey: ['public-tracking', 'customer', customerCode, ...bills],
    queryFn: () =>
      http.post<{ data: TrackResult[] }>(`/public/tracking/customers/${encodeURIComponent(customerCode)}`, { bills }).then(r => r.data),
    enabled: bills.length > 0,
    staleTime: 60_000,
    retry: false
  });
}
