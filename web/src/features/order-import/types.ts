/** 1 dòng kết quả kiểm tra / tạo đơn — POST /orders/import/preview và /orders/import. */
export interface ImportRow {
  /** Số dòng trong file Excel (dữ liệu bắt đầu từ dòng 3). */
  line: number;
  ref: string;
  type: 'DOC' | 'PACK';
  consignee: string;
  countryCode: string;
  country: string;
  city: string;
  pieces: number;
  weightKg: number;
  /** Tổng cân tính cước từng kiện (D×R×C / 5000), làm tròn theo hãng/dịch vụ. */
  chargeableKg: number;
  value: number;
  currency: string;
  products: number;
  errors: string[];
  warnings: string[];
  /** Số vận đơn — chỉ có sau khi tạo đơn. */
  bill?: string | null;
}

export interface ImportResult {
  total: number;
  valid: number;
  invalid: number;
  created: number;
  rows: ImportRow[];
}

/** Dịch vụ áp cho cả file — không bắt buộc; đã chọn dịch vụ thì phải chọn hub. */
export interface ImportDefaults {
  service: string;
  hub: string;
  branch: string;
}
