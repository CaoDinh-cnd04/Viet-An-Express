import type { NewTrouble, TroubleKind, TroublePriority } from '../types';

/** Giá trị form gửi yêu cầu trên trang Hỗ trợ & sự cố (dạng chữ như ô nhập). */
export interface TroubleFormValues {
  bill: string;
  type: string;
  subject: string;
  lv: TroublePriority;
  desc: string;
  req: string;
  contact: string;
}

/** Khớp kiểm tra của backend (TroubleRules.Validate). */
export const DESC_MIN = 10;
export const SUBJECT_MIN = 5;

/**
 * Lỗi đầu tiên của form theo loại yêu cầu (câu tiếng Việt để hiện ngay dưới form), không lỗi → null. Hàm thuần — có test.
 * Sự cố: vận đơn, vấn đề, mô tả, người yêu cầu, liên hệ. Hỗ trợ: chủ đề, tiêu đề, nội dung, liên hệ. Góp ý: chỉ nội dung.
 */
export function troubleFormError(kind: TroubleKind, v: TroubleFormValues): string | null {
  if (kind === 'su_co' && !v.bill.trim()) return 'Nhập mã vận đơn';
  if (kind !== 'gop_y' && !v.type.trim()) return kind === 'su_co' ? 'Chọn vấn đề của đơn' : 'Chọn chủ đề';
  if (kind === 'ho_tro' && v.subject.trim().length < SUBJECT_MIN) return 'Tiêu đề tối thiểu 5 ký tự';
  if (v.desc.trim().length < DESC_MIN) return 'Nội dung tối thiểu 10 ký tự để CS xử lý nhanh';
  if (kind === 'su_co' && !v.req.trim()) return 'Nhập người yêu cầu';
  if (kind !== 'gop_y' && !v.contact.trim()) return 'Nhập SĐT hoặc email liên hệ';
  return null;
}

/** Dữ liệu gửi POST /troubles: chỉ gửi trường của loại đó, cắt khoảng trắng. */
export function toNewTrouble(kind: TroubleKind, v: TroubleFormValues, file: File | null): NewTrouble {
  return {
    kind,
    bill: kind === 'su_co' ? v.bill.trim() : undefined,
    type: kind === 'gop_y' ? undefined : v.type,
    subject: kind === 'ho_tro' ? v.subject.trim() : undefined,
    lv: kind === 'su_co' ? v.lv : undefined,
    desc: v.desc.trim(),
    req: v.req.trim(),
    contact: v.contact.trim(),
    file
  };
}
