import type { OrderEvent, OrderStatus } from '@/shared/model/shipment';

/**
 * Kết quả tra cứu 1 vận đơn trên trang ngoài — POST /public/tracking.
 * Các trường đánh dấu "tuỳ chọn" backend bổ sung dần; thiếu thì giao diện hiện "Đang cập nhật".
 */
export type TrackResult =
  | {
      bill: string;
      found: true;
      status: OrderStatus;
      /** Nước / thành phố đến, vd "LONDON, United Kingdom". */
      destination?: string;
      /** Dịch vụ / hãng, vd "DHL". */
      service?: string;
      /** Hành trình, mới nhất trước. `time`: "dd/MM/yyyy HH:mm" hoặc "dd/MM/yyyy". */
      events: OrderEvent[];
      /** Tuỳ chọn — nơi gửi, vd "HCMC, Vietnam". */
      origin?: string;
      /** Tuỳ chọn — ngày gửi "dd/MM/yyyy". */
      shipDate?: string;
      /** Tuỳ chọn — ngày giao dự kiến "dd/MM/yyyy". */
      estimatedDate?: string;
      /** Tuỳ chọn — số kiện. */
      pieces?: number;
      /** Tuỳ chọn — cân tính cước (kg). */
      weightKg?: number;
      /** Tuỳ chọn — mã vận đơn của hãng (khi tra bằng số VA). */
      carrierBill?: string;
    }
  | { bill: string; found: false };

export type FoundTrackResult = Extract<TrackResult, { found: true }>;
