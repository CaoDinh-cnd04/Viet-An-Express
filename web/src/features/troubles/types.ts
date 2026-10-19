/** Hỗ trợ & sự cố — hợp đồng API: docs/API_CONTRACT.md §6. Lưu ở bảng cũ dbo.TContact, không có trao đổi qua lại. */
/** su_co = sự cố 1 vận đơn, ho_tro = yêu cầu hỗ trợ chung, gop_y = góp ý. */
export type TroubleKind = 'su_co' | 'ho_tro' | 'gop_y';
export type TroublePriority = 'low' | 'mid' | 'high';
export type TroubleStatus = 'new' | 'doing' | 'waitc' | 'done';

export interface TroubleTicket {
  /** VD TRB-1024 */
  id: string;
  /** Thiếu = sự cố (backend cũ). */
  kind?: TroubleKind;
  /** Mã vận đơn — chỉ sự cố có, hỗ trợ / góp ý để trống. */
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
  /** Bắt buộc với sự cố. */
  bill?: string;
  cnee?: string;
  ct?: string;
  /** Loại sự cố / chủ đề hỗ trợ; góp ý để trống. */
  type?: string;
  /** Bắt buộc với yêu cầu hỗ trợ. */
  subject?: string;
  /** Bắt buộc với sự cố. */
  lv?: TroublePriority;
  desc: string;
  req?: string;
  contact?: string;
  /** Ảnh / chứng từ đính kèm (≤ 10 MB) — chỉ gửi kèm email cho CS, không lưu trên portal. */
  file?: File | null;
}
