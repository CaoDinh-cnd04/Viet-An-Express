import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { http } from '@/shared/api/http';
import { parseTags, serializeTags } from './lib/multi-filter';
import type { OrderEvent } from '@/shared/model/shipment';
import { ordersApiBase, useOrdersSource, type OrdersSource } from './source';
import type { ExportScope, OrderDetail, OrderFilters, OrderListResponse, OrderPhoto } from './types';

export const orderKeys = {
  all: ['orders'] as const,
  list: (filters: OrderFilters, source: OrdersSource = 'mine') => [...orderKeys.all, 'list', source, filters] as const,
  detail: (ref: string, source: OrdersSource = 'mine') => [...orderKeys.all, 'detail', source, ref] as const
};

const orderPath = (source: OrdersSource, bill: string, rest = '') => `${ordersApiBase(source)}/${encodeURIComponent(bill)}${rest}`;

export function fetchOrders(f: OrderFilters, source: OrdersSource = 'mine'): Promise<OrderListResponse> {
  return http.get<OrderListResponse>(ordersApiBase(source), {
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
  const source = useOrdersSource();
  return useQuery({
    queryKey: orderKeys.list(filters, source),
    queryFn: () => fetchOrders(filters, source),
    placeholderData: keepPreviousData
  });
}

export function useOrder(ref: string | null) {
  const source = useOrdersSource();
  return useQuery({
    queryKey: orderKeys.detail(ref ?? '', source),
    queryFn: () => http.get<{ data: OrderDetail }>(orderPath(source, ref ?? '')).then(r => r.data),
    enabled: !!ref
  });
}

/** Ảnh kiện hàng — chưa dùng: backend chưa làm (API_CONTRACT.md "Chưa làm"), hộp Ảnh kiện đang báo "đang phát triển". */
export function useOrderPhotos(bill: string | null) {
  const source = useOrdersSource();
  return useQuery({
    queryKey: [...orderKeys.detail(bill ?? '', source), 'photos'],
    queryFn: () => http.get<{ data: OrderPhoto[] }>(orderPath(source, bill ?? '', '/photos')).then(r => r.data),
    enabled: !!bill,
    retry: false
  });
}

/** Hành trình đơn. Endpoint mới — xem API_CONTRACT.md. */
export function useOrderEvents(bill: string | null) {
  const source = useOrdersSource();
  return useQuery({
    queryKey: [...orderKeys.detail(bill ?? '', source), 'events'],
    queryFn: () => http.get<{ data: OrderEvent[] }>(orderPath(source, bill ?? '', '/events')).then(r => r.data),
    enabled: !!bill,
    retry: false
  });
}

/**
 * Trang HTML in chứng từ (bill A4, invoice, CVCK, nhãn A6) cho 1 hoặc nhiều đơn — GET /orders/print.
 * `duty` (DDU / DDP): chỉ có khi in ngay lúc tạo đơn — in lên invoice, không lưu.
 */
export function fetchPrintHtml(bills: readonly string[], doc: string, duty?: string, source: OrdersSource = 'mine'): Promise<string> {
  return http.getText(`${ordersApiBase(source)}/print`, { bills: bills.join(','), doc, ...(duty ? { duty } : {}) });
}

/** File Excel bảng kê gửi hàng theo bộ lọc hiện tại — GET /orders/export. `page`: chỉ trang đang xem; `all`: toàn bộ. */
export function fetchOrdersExport(f: OrderFilters, scope: ExportScope, source: OrdersSource = 'mine') {
  return http.getFile(`${ordersApiBase(source)}/export`, {
    ...(scope === 'page' && { page: f.page, pageSize: f.pageSize }),
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
