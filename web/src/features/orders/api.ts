import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import { parseTags, serializeTags } from './lib/multi-filter';
import type { OrderDetail, OrderEvent, OrderFilters, OrderListResponse, OrderPhoto } from './types';
import type { CreateOrderValues } from '@/features/create-order/schema';

export const orderKeys = {
  all: ['orders'] as const,
  list: (filters: OrderFilters) => [...orderKeys.all, 'list', filters] as const,
  detail: (ref: string) => [...orderKeys.all, 'detail', ref] as const
};

export function fetchOrders(f: OrderFilters): Promise<OrderListResponse> {
  return http.get<OrderListResponse>('/orders', {
    q: serializeTags(parseTags(f.q, f.searchField)),
    searchField: f.searchField,
    status: f.status,
    type: f.type,
    fromDate: f.fromDate,
    toDate: f.toDate,
    weightFrom: f.weightFrom,
    weightTo: f.weightTo,
    page: f.page,
    pageSize: f.pageSize,
    sortBy: f.sortBy,
    sortDir: f.sortDir
  });
}

/** Danh sách đơn theo bộ lọc; giữ dữ liệu trang trước khi đang tải trang mới để bảng không nhấp nháy. */
export function useOrders(filters: OrderFilters) {
  return useQuery({
    queryKey: orderKeys.list(filters),
    queryFn: () => fetchOrders(filters),
    placeholderData: keepPreviousData
  });
}

export function useOrder(ref: string | null) {
  return useQuery({
    queryKey: orderKeys.detail(ref ?? ''),
    queryFn: () => http.get<{ data: OrderDetail }>(`/orders/${encodeURIComponent(ref ?? '')}`).then(r => r.data),
    enabled: !!ref
  });
}

/** Full saved form for a new copy, including packages, invoice, addons and original precision. */
export function useOrderCopy(bill: string | null) {
  return useQuery({
    queryKey: [...orderKeys.detail(bill ?? ''), 'copy'],
    queryFn: () => http.get<{ data: Partial<CreateOrderValues> }>(`/orders/${encodeURIComponent(bill ?? '')}/copy`).then(r => r.data),
    enabled: !!bill,
    retry: false
  });
}

/** Ảnh kiện hàng. Endpoint mới — xem API_CONTRACT.md. */
export function useOrderPhotos(bill: string | null) {
  return useQuery({
    queryKey: [...orderKeys.detail(bill ?? ''), 'photos'],
    queryFn: () => http.get<{ data: OrderPhoto[] }>(`/orders/${encodeURIComponent(bill ?? '')}/photos`).then(r => r.data),
    enabled: !!bill,
    retry: false
  });
}

/** Hành trình đơn. Endpoint mới — xem API_CONTRACT.md. */
export function useOrderEvents(bill: string | null) {
  return useQuery({
    queryKey: [...orderKeys.detail(bill ?? ''), 'events'],
    queryFn: () => http.get<{ data: OrderEvent[] }>(`/orders/${encodeURIComponent(bill ?? '')}/events`).then(r => r.data),
    enabled: !!bill,
    retry: false
  });
}

/** Trang HTML in chứng từ (bill A4, invoice, CVCK, nhãn A6) cho 1 hoặc nhiều đơn — GET /orders/print. */
export function fetchPrintHtml(bills: readonly string[], doc: string): Promise<string> {
  return http.getText('/orders/print', { bills: bills.join(','), doc });
}

/** File Excel bảng kê gửi hàng theo bộ lọc hiện tại — GET /orders/export. */
export function fetchOrdersExport(f: OrderFilters) {
  return http.getFile('/orders/export', {
    q: serializeTags(parseTags(f.q, f.searchField)),
    searchField: f.searchField,
    status: f.status,
    type: f.type,
    fromDate: f.fromDate,
    toDate: f.toDate,
    weightFrom: f.weightFrom,
    weightTo: f.weightTo,
    sortBy: f.sortBy,
    sortDir: f.sortDir
  });
}
