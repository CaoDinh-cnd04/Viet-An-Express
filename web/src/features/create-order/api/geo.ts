import { useQuery } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import type { AddressSuggestion, Country, PostalSuggestion } from '../lib/geo';
import type { RemoteAreaHit } from '../lib/remote-area';
import { ONE_HOUR } from './cache';

// Địa lý: quốc gia, mã bưu chính, vùng sâu vùng xa, gợi ý địa chỉ người nhận.

const ONE_DAY = 24 * ONE_HOUR;

/** Danh sách quốc gia + mã điện thoại (backend lấy từ bộ dữ liệu world-countries, có cache). */
export function useCountries() {
  return useQuery({
    queryKey: ['geo', 'countries'],
    queryFn: () => http.get<{ data: Country[] }>('/geo/countries').then(r => r.data),
    staleTime: ONE_DAY,
    gcTime: ONE_DAY,
    retry: 1
  });
}

/** Gợi ý mã bưu chính khi khách gõ (backend gọi GeoNames, frontend không giữ tài khoản GeoNames). Nguồn lỗi → []. */
export function usePostalSearch(countryCode: string | undefined, query: string | null) {
  return useQuery({
    queryKey: ['geo', 'postal-search', countryCode, query],
    queryFn: () =>
      http
        .get<{ data: PostalSuggestion[] }>('/geo/postal-search', { country: countryCode, q: query })
        .then(r => r.data),
    enabled: !!countryCode && countryCode.length === 2 && !!query,
    staleTime: ONE_DAY,
    retry: false
  });
}

/** Hãng tính phụ phí vùng sâu vùng xa ở địa chỉ người nhận (backend tra bảng VSVX của hệ thống cũ). Lỗi → không cảnh báo. */
export function useRemoteAreas(countryCode: string | undefined, postal: string | null, city: string) {
  return useQuery({
    queryKey: ['geo', 'remote-areas', countryCode, postal, city],
    queryFn: () =>
      http.get<{ data: RemoteAreaHit[] }>('/geo/remote-areas', { country: countryCode, postal, city }).then(r => r.data),
    enabled: countryCode?.length === 2 && !!postal,
    staleTime: ONE_DAY,
    retry: false
  });
}

/** Gợi ý địa chỉ người nhận trong nước đến (backend gọi Geoapify, chưa cấu hình thì trả rỗng). Endpoint mới — API_CONTRACT.md §3. */
export function useAddressSuggestions(countryCode: string | undefined, query: string | null) {
  return useQuery({
    queryKey: ['geo', 'addresses', countryCode, query],
    queryFn: () =>
      http
        .get<{ data: AddressSuggestion[] }>('/geo/addresses', { country: countryCode, q: query })
        .then(r => r.data),
    enabled: countryCode?.length === 2 && !!query,
    staleTime: ONE_DAY,
    placeholderData: previous => previous,
    retry: false
  });
}
