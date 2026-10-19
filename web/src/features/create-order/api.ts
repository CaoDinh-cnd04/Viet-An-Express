import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import type { AddressSuggestion, Country, PostalSuggestion } from './lib/geo';
import type { RecentReceiver } from './lib/recent-receivers';
import type { RecentSender } from './lib/recent-senders';
import type { RemoteAreaHit } from './lib/remote-area';
import type { InvoiceItemValues } from './schema';

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

/** Đổi sao trong cache ngay khi bấm; trả dữ liệu cũ để hoàn lại nếu máy chủ báo lỗi. */
async function toggleCached<T extends { id?: string; isFavorite?: boolean }>(qc: QueryClient, key: readonly string[], id: string, isFavorite: boolean) {
  await qc.cancelQueries({ queryKey: key });
  const previous = qc.getQueryData<T[]>(key);
  qc.setQueryData<T[]>(key, list => list?.map(x => (x.id === id ? { ...x, isFavorite } : x)));
  return previous;
}

/** Hồ sơ người gửi (backend SenderProfile). */
export interface SenderProfile {
  id?: string;
  n: string;
  c: string;
  t: string;
  d: string;
}

const ONE_HOUR = 60 * 60 * 1000;

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

export function useSenders() {
  return useQuery({
    queryKey: ['addresses', 'senders'],
    queryFn: () => http.get<{ data: SenderProfile[] }>('/addresses/senders').then(r => r.data)
  });
}

/** Mặt hàng khách đã lưu để khai invoice nhanh (thư viện mặt hàng). */
export interface SavedProduct {
  /** Khóa mặt hàng trong thư viện (dùng để đánh dấu yêu thích / xóa). */
  id?: string;
  /** Mặt hàng khách đánh dấu sao — đứng đầu thư viện. */
  isFavorite?: boolean;
  descEn: string;
  descVi: string;
  manufacturer: string;
  origin: string;
  hs: string;
  unit: string;
  price: string;
}

/** Invoice của các đơn gần đây — để chép lại khi gửi hàng lặp lại. */
export interface RecentInvoice {
  bill: string;
  cnee: string;
  date: string;
  currency: string;
  items: SavedProduct[];
}

/** Biểu phí 1 tùy chọn dịch vụ — `name` khớp ADDONS.name (giá trị lưu trong đơn). */
export interface AddonFee {
  name: string;
  /** Phí; null = chưa có giá / liên hệ CS. */
  fee: number | null;
  currency: string;
  /** Cách tính, vd "đơn", "kiện", "% giá trị hàng". */
  unit: string;
  /** Ghi chú (mức tối thiểu, điều kiện…). */
  note?: string | null;
}

/** Biểu phí tùy chọn dịch vụ — GET /catalog/addon-fees. Endpoint mới (API_CONTRACT §3); chưa có thì UI báo "đang cập nhật". */
export function useAddonFees(enabled: boolean) {
  return useQuery({
    queryKey: ['catalog', 'addon-fees'],
    queryFn: () => http.get<{ data: AddonFee[] }>('/catalog/addon-fees').then(r => r.data),
    enabled,
    staleTime: ONE_HOUR,
    retry: false
  });
}

const PRODUCTS_KEY = ['catalog', 'products'] as const;

/** Thư viện mặt hàng: yêu thích đứng đầu, mặt hàng đã xóa không trả về. */
export function useProductLibrary(enabled: boolean) {
  return useQuery({
    queryKey: PRODUCTS_KEY,
    queryFn: () => http.get<{ data: SavedProduct[] }>('/catalog/products').then(r => r.data),
    enabled,
    retry: false
  });
}

/** Đánh dấu / bỏ yêu thích mặt hàng trong thư viện. */
export function useFavoriteProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isFavorite }: { id: string; isFavorite: boolean }) =>
      http.put<{ message: string }>(`/catalog/products/${encodeURIComponent(id)}/favorite`, { isFavorite }),
    onMutate: ({ id, isFavorite }) => toggleCached<SavedProduct>(qc, PRODUCTS_KEY, id, isFavorite),
    onError: (_e, _v, previous) => qc.setQueryData(PRODUCTS_KEY, previous),
    onSettled: () => void qc.invalidateQueries({ queryKey: PRODUCTS_KEY })
  });
}

/** Xóa mặt hàng khỏi thư viện (chỉ ẩn — đơn cũ giữ nguyên). */
export function useDeleteProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => http.delete<{ message: string }>(`/catalog/products/${encodeURIComponent(id)}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: PRODUCTS_KEY })
  });
}

export function useRecentInvoices(enabled: boolean) {
  return useQuery({
    queryKey: ['invoices', 'recent'],
    queryFn: () => http.get<{ data: RecentInvoice[] }>('/invoices/recent', { limit: 20 }).then(r => r.data),
    enabled,
    retry: false
  });
}

/** 1 mặt hàng trong tra cứu mã HS theo nước nhập khẩu — GET /catalog/hs-lookup. */
export interface HsLookupItem {
  nameVi: string;
  nameEn: string;
  /** 6 số quốc tế. */
  hs6: string;
  /** Mã quốc gia đầy đủ (US 10 số HTSUS, UK 10 số, AU 8/10 số) — null nếu chưa có. */
  nationalCode?: string | null;
  /** Mô tả dòng mã trong biểu thuế của nước đó. */
  nationalDescription?: string | null;
  /** Ghi chú của Việt An (chất liệu, quy cách đóng gói ảnh hưởng mã…). */
  note?: string | null;
  /** Đơn vị tính thống kê của biểu thuế (kg, No., doz., kg…). */
  unit?: string | null;
  /** Mặt hàng Việt An đã gửi thực tế và rà soát mã. */
  isVerified?: boolean;
  /** Các cấp cha trong biểu thuế (US), từ nhóm lớn đến nhỏ — rỗng nếu biểu thuế không có. */
  path?: string[];
}

/**
 * Tra mã HS theo nước nhập khẩu từ dbo.Catalog_HsCode (`mode=verified`: chỉ mặt hàng Việt An đã xác minh;
 * `mode=catalog`: toàn bộ biểu thuế nước đó) — API_CONTRACT §3. Backend chưa có endpoint thì UI dùng dữ liệu HS6 sẵn có.
 */
export function useHsLookup(country: string, query: string, mode: 'verified' | 'catalog') {
  return useQuery({
    queryKey: ['catalog', 'hs-lookup', country, mode, query],
    queryFn: () => http.get<{ data: HsLookupItem[] }>('/catalog/hs-lookup', { country, q: query, mode }).then(r => r.data),
    enabled: query.trim().length >= 2,
    staleTime: 10 * 60_000,
    retry: false
  });
}

/** File Excel mẫu invoice (Mau_Import_Invoice.xlsx) — tải từ nút "Tải file mẫu" ở khung Hỗ trợ khách hàng. */
export const INVOICE_TEMPLATE_URL = '/templates/Mau_Import_Invoice.xlsx';

/** Đọc file Excel invoice ở backend (ClosedXML) → dòng hàng + dòng lỗi. POST /invoices/import (multipart `file`), không lưu gì. */
export function importInvoiceFile(file: File) {
  const form = new FormData();
  form.append('file', file, file.name);
  return http
    .post<{ data: { items: InvoiceItemValues[]; skipped: string[] }; message?: string }>('/invoices/import', form)
    .then(r => r.data);
}

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

/** Người nhận đã gửi trước đây có tên công ty chứa `q` (đơn cũ của khách). API_CONTRACT.md §1. */
export function useRecentReceivers(query: string | null) {
  return useQuery({
    queryKey: ['orders', 'receivers', query],
    queryFn: () => http.get<{ data: RecentReceiver[] }>('/orders/receivers', { q: query }).then(r => r.data),
    enabled: !!query,
    staleTime: 60_000,
    placeholderData: previous => previous,
    retry: false
  });
}

/** Người gửi đã dùng ở đơn trước, tên công ty / người liên hệ chứa `query` (gợi ý cho tài khoản con khai người gửi). */
export function useRecentSenders(query: string | null) {
  return useQuery({
    queryKey: ['orders', 'senders', query],
    queryFn: () => http.get<{ data: RecentSender[] }>('/orders/senders', { q: query }).then(r => r.data),
    enabled: !!query,
    staleTime: 60_000,
    placeholderData: previous => previous,
    retry: false
  });
}

/** Sổ địa chỉ người nhận: 100 người nhận gần nhất từ đơn cũ của khách (GET /orders/receivers không có q). */
export function useReceiverBook(enabled: boolean) {
  return useQuery({
    queryKey: ['orders', 'receivers', 'book'],
    queryFn: () => http.get<{ data: RecentReceiver[] }>('/orders/receivers').then(r => r.data),
    enabled,
    staleTime: 60_000
  });
}
