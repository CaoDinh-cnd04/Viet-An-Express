import type { Tone } from '@/shared/ui';
import type { OrderFilters, OrderSearchField, OrderStatus } from './types';

export const ORDER_STATUS: Record<OrderStatus, { label: string; tone: Tone }> = {
  wait: { label: 'Chưa đi', tone: 'info' },
  fly: { label: 'Đã đi', tone: 'success' },
  nd: { label: 'Chưa phát', tone: 'warning' },
  ok: { label: 'Đã phát', tone: 'brand' }
};

export const SEARCH_FIELDS: ReadonlyArray<{ value: OrderSearchField; label: string }> = [
  { value: 'all', label: 'Tất cả' },
  { value: 'cnee', label: 'Tên người nhận' },
  { value: 'bill', label: 'VA Bill' },
  { value: 'ref', label: 'Số tham chiếu' },
  { value: 'ct', label: 'Nước đến' }
];

export const PAGE_SIZES = [20, 50, 100] as const;

export const DEFAULT_FILTERS: OrderFilters = {
  q: '',
  searchField: 'all',
  status: 'all',
  type: '',
  fromDate: '',
  toDate: '',
  weightFrom: '',
  weightTo: '',
  page: 1,
  pageSize: 20,
  sortBy: 'seq',
  sortDir: 'desc'
};

/** Số ngày vận chuyển tham khảo theo hãng — dùng ước tính POD khi backend chưa trả về. */
export const TRANSIT_DAYS: ReadonlyArray<[prefix: string, days: number]> = [
  ['DHL', 3],
  ['Fedex', 3],
  ['UPS', 4],
  ['Aramex', 4],
  ['Chuyên tuyến', 5],
  ['Ecom', 7],
  ['SEA', 25]
];
export const DEFAULT_TRANSIT_DAYS = 5;

export const PRINT_DOCUMENTS = [
  { key: 'bill-a4', label: 'In vận đơn (Bill A4)' },
  { key: 'invoice', label: 'In hóa đơn (Invoice)' },
  { key: 'cvck', label: 'Công văn cam kết (CVCK)' },
  { key: 'label-a6', label: 'In nhãn (A6)' }
] as const;
