/**
 * Hỗ trợ & sự cố — hợp đồng API: docs/API_CONTRACT.md §6. Mỗi yêu cầu (mã TRB-…) = 1 dòng bảng cũ dbo.TContact, như form "Yêu cầu"
 * của Bill Online cũ. CS trả lời / khách trả lời lại nối vào cuối nội dung TContact (không có bảng riêng, không gửi thông báo).
 */
/** ho_tro = hỗ trợ & sự cố (gộp 1, mã vận đơn không bắt buộc), gop_y = góp ý; su_co = sự cố gửi trước 27/10/2026 (chỉ còn để hiển thị). */
export type SupportKind = 'su_co' | 'ho_tro' | 'gop_y';
export type SupportPriority = 'low' | 'mid' | 'high';
export type SupportStatus = 'new' | 'doing' | 'waitc' | 'done';

export interface SupportTicket {
  /** VD TRB-1024 */
  id: string;
  /** Thiếu = sự cố (backend cũ). */
  kind?: SupportKind;
  /** Mã vận đơn — có khi khách hỏi về 1 vận đơn, không thì trống. */
  bill: string;
  cnee: string;
  ct: string;
  /** Loại sự cố, hoặc chủ đề của yêu cầu hỗ trợ / góp ý. */
  type: string;
  /** Tiêu đề — yêu cầu hỗ trợ. */
  subject?: string | null;
  lv: SupportPriority;
  desc: string;
  /** Người yêu cầu */
  req: string;
  contact: string;
  date: string;
  status: SupportStatus;
  /** Lần đổi trạng thái / trả lời gần nhất — 'dd/MM/yyyy HH:mm'. */
  updatedAt?: string;
  /** Các lần CS / khách trả lời, cũ trước mới sau (thiếu = backend cũ, chưa có trao đổi). */
  thread?: SupportMessage[];
}

/** 1 lần trả lời trên yêu cầu: cs = CS Việt An, customer = khách. */
export interface SupportMessage {
  from: 'cs' | 'customer';
  /** 'dd/MM/yyyy HH:mm' */
  at: string;
  text: string;
}

export interface NewSupportRequest {
  kind: SupportKind;
  /** Không bắt buộc — có thì phải là vận đơn của khách. */
  bill?: string;
  cnee?: string;
  ct?: string;
  /** Loại sự cố / chủ đề hỗ trợ; góp ý để trống. */
  type?: string;
  /** Không bắt buộc — trống thì tiêu đề = vấn đề. */
  subject?: string;
  lv?: SupportPriority;
  desc: string;
  req?: string;
  contact?: string;
  /** Ảnh / chứng từ đính kèm (≤ 10 MB) — chỉ gửi kèm email cho CS, không lưu trên portal. */
  file?: File | null;
}
