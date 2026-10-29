import type { NewSupportRequest, SupportKind } from '../types';

/** Nội dung đang soạn ở form "Gửi yêu cầu mới". */
export interface RequestDraft {
  kind: SupportKind;
  bill: string;
  /** Vấn đề (HELP_TOPICS) — hỗ trợ & sự cố; nội dung góp ý (FEEDBACK_TOPICS) — góp ý. */
  topic: string;
  /** Mức hài lòng 1–5 (góp ý). */
  rating: number | null;
  text: string;
  urgent: boolean;
  /** Người liên hệ + SĐT / email — lấy từ tài khoản. */
  requester: string;
  contact: string;
}

/** Mở sẵn loại / vận đơn / vấn đề (link từ đơn hàng, form tạo đơn). */
export interface RequestPreset {
  kind?: SupportKind;
  bill?: string;
  topic?: string;
}

/** Form trống theo loại; sự cố cũ (su_co) gộp vào hỗ trợ & sự cố. Liên hệ điền sẵn từ tài khoản. */
export function newDraft(preset: RequestPreset, contact: { requester: string; contact: string }): RequestDraft {
  const kind: SupportKind = preset.kind === 'gop_y' ? 'gop_y' : 'ho_tro';
  return {
    kind,
    bill: kind === 'ho_tro' ? preset.bill ?? '' : '',
    topic: preset.topic ?? '',
    rating: null,
    text: '',
    urgent: false,
    ...contact
  };
}

/** Chữ ghi vào nội dung gửi CS (dữ liệu CS đọc ở phần mềm cũ — không dịch, xem NO_TRANSLATE). */
const DATA = { bill: 'vận đơn', rating: 'Mức hài lòng', feedback: 'Góp ý' } as const;

/** Khớp backend (SupportRequestRules.Validate). */
export const DESC_MIN = 10;

/** Lỗi đầu tiên (câu tiếng Việt) hoặc null. */
export function draftError(d: RequestDraft): string | null {
  if (!d.topic) return d.kind === 'gop_y' ? 'Chọn nội dung góp ý' : 'Chọn vấn đề cần hỗ trợ';
  // Có mã vận đơn thì nội dung không bắt buộc (tự ghi "{vấn đề} — vận đơn {mã}").
  if (!d.text.trim() && (d.kind === 'gop_y' || !d.bill.trim())) return d.kind === 'gop_y' ? 'Viết góp ý của bạn' : 'Viết nội dung cần hỗ trợ';
  if (d.kind !== 'gop_y' && !d.contact.trim()) return 'Nhập SĐT hoặc email để CS liên hệ';
  return null;
}

/**
 * Form → POST /troubles. Hỗ trợ & sự cố: mã vận đơn không bắt buộc; nội dung ngắn / trống thì ghép vấn đề (+ vận đơn) cho đủ
 * ≥ 10 ký tự backend cần; tiêu đề = vấn đề (như form "Yêu cầu" cũ). Góp ý: mức hài lòng ghi đầu nội dung để CS đọc ở phần mềm cũ.
 * Gấp → mức độ "Rất gấp", không thì "Thường".
 */
export function toNewSupportRequest(d: RequestDraft, file: File | null): NewSupportRequest {
  const text = d.text.trim();
  const bill = d.bill.trim();
  if (d.kind !== 'gop_y') {
    const head = bill ? `${d.topic} — ${DATA.bill} ${bill}` : d.topic;
    const desc = text.length >= DESC_MIN ? text : [head, text].filter(Boolean).join('. ');
    return { kind: 'ho_tro', bill: bill || undefined, type: d.topic, lv: d.urgent ? 'high' : 'low', desc, req: d.requester.trim(), contact: d.contact.trim(), file };
  }
  const rated = d.rating ? `${DATA.rating}: ${d.rating}/5\n${text}` : text;
  const desc = rated.length >= DESC_MIN ? rated : `${DATA.feedback}: ${rated}`;
  return { kind: 'gop_y', type: d.topic, desc, req: d.requester.trim(), contact: d.contact.trim(), file };
}
