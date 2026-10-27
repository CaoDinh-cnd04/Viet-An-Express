/**
 * Hỗ trợ & sự cố — hợp đồng API: docs/API_CONTRACT.md §6. Mỗi yêu cầu (mã TRB-…) = 1 dòng bảng cũ dbo.TContact, như form "Yêu cầu"
 * của Bill Online cũ; CS phản hồi qua thông báo riêng (không nhắn qua lại).
 */
/** ho_tro = hỗ trợ & sự cố (gộp 1, mã vận đơn không bắt buộc), gop_y = góp ý; su_co = sự cố gửi trước 27/10/2026 (chỉ còn để hiển thị). */
export type TroubleKind = 'su_co' | 'ho_tro' | 'gop_y';
export type TroublePriority = 'low' | 'mid' | 'high';
export type TroubleStatus = 'new' | 'doing' | 'waitc' | 'done';

export interface TroubleTicket {
  /** VD TRB-1024 */
  id: string;
  /** Thiếu = sự cố (backend cũ). */
  kind?: TroubleKind;
  /** Mã vận đơn — có khi khách hỏi về 1 vận đơn, không thì trống. */
  bill: string;
  cnee: string;
  ct: string;
  /** Loại sự cố, hoặc chủ đề của yêu cầu hỗ trợ / góp ý. */
  type: string;
  /** Tiêu đề — yêu cầu hỗ trợ. */
  subject?: string | null;
  lv: TroublePriority;
  desc: string;
  /** Người yêu cầu */
  req: string;
  contact: string;
  date: string;
  status: TroubleStatus;
  /** Lần đổi trạng thái gần nhất — 'dd/MM/yyyy HH:mm'. */
  updatedAt?: string;
}

export interface NewTrouble {
  kind: TroubleKind;
  /** Không bắt buộc — có thì phải là vận đơn của khách. */
  bill?: string;
  cnee?: string;
  ct?: string;
  /** Loại sự cố / chủ đề hỗ trợ; góp ý để trống. */
  type?: string;
  /** Không bắt buộc — trống thì tiêu đề = vấn đề. */
  subject?: string;
  lv?: TroublePriority;
  desc: string;
  req?: string;
  contact?: string;
  /** Ảnh / chứng từ đính kèm (≤ 10 MB) — chỉ gửi kèm email cho CS, không lưu trên portal. */
  file?: File | null;
}
