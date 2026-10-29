import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import type { InvoiceItemValues } from '@/features/order-form';
import { toggleCached, ONE_HOUR } from './cache';

// Khai invoice nhanh: thư viện mặt hàng, invoice gần đây, biểu phí tùy chọn dịch vụ, tra mã HS, đọc file invoice.

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
