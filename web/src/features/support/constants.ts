import { CONTACTS as COMPANY_CONTACTS } from '@/shared/config/company';
import type { IconName, Tone } from '@/shared/ui';
import type { TicketFilter } from './lib/requests';
import type { SupportKind, SupportPriority, SupportStatus } from './types';

/** Loại yêu cầu: khách chọn Hỗ trợ & sự cố hoặc Góp ý; "Sự cố vận đơn" chỉ còn cho yêu cầu gửi trước 27/10/2026. */
export const SUPPORT_KINDS: Record<SupportKind, { label: string; hint: string; icon: IconName; tone: Tone }> = {
  su_co: { label: 'Sự cố vận đơn', hint: 'Giao chậm, thất lạc, hư hỏng, phụ phí… của 1 đơn hàng', icon: 'alert', tone: 'danger' },
  ho_tro: { label: 'Hỗ trợ & sự cố', hint: 'Sự cố đơn hàng, tracking, cước, tạo đơn, sửa thông tin…', icon: 'help', tone: 'info' },
  gop_y: { label: 'Góp ý', hint: 'Dịch vụ, nhân viên, phần mềm', icon: 'message', tone: 'brand' }
};

export const SUPPORT_KIND_ORDER: readonly SupportKind[] = ['ho_tro', 'gop_y'];

/** Nhãn ngắn ở cột Loại của bảng yêu cầu. */
export const SUPPORT_KIND_SHORT: Record<SupportKind, string> = { su_co: 'Sự cố', ho_tro: 'Hỗ trợ', gop_y: 'Góp ý' };

/** Vấn đề khách chọn khi gửi Hỗ trợ & sự cố (gộp loại sự cố + chủ đề hỗ trợ) — lưu ở TContact.KeyWord. */
export const HELP_TOPICS = [
  'Giao chậm / trễ hẹn',
  'Thất lạc kiện hàng',
  'Hư hỏng / vỡ hàng',
  'Thiếu hàng trong kiện',
  'Sai thông tin người nhận',
  'Phát sai địa chỉ',
  'Yêu cầu giữ / đổi địa chỉ',
  'Cước & phụ phí',
  'Tra cứu / tracking',
  'Hỗ trợ tạo đơn',
  'Sửa thông tin người gửi',
  'Báo lỗi phần mềm',
  'Khác'
] as const;

/** Nội dung góp ý — đúng 4 mục ô "Chọn nội dung góp ý" của form "Yêu cầu" (Bill Online cũ); lưu TContact.KeyWord như form cũ. */
export const FEEDBACK_TOPICS = ['Chỉnh sửa phần mềm', 'Cập nhật thông tin', 'Góp ý nâng cấp', 'Khác'] as const;

/** Chủ đề mở sẵn từ nút "Sửa hồ sơ người gửi" (form tạo đơn). */
export const SENDER_INFO_TOPIC = 'Sửa thông tin người gửi';

/** Tệp đính kèm: tối đa 10 MB, ảnh / PDF / Excel / CSV / Word (khớp backend). */
export const FILE_MAX_MB = 10;
export const FILE_ACCEPT = 'image/*,.pdf,.xlsx,.xls,.csv,.doc,.docx';

export const SUPPORT_PRIORITY: Record<SupportPriority, { label: string; tone: Tone }> = {
  low: { label: 'Thường', tone: 'neutral' },
  mid: { label: 'Gấp', tone: 'warning' },
  high: { label: 'Rất gấp', tone: 'danger' }
};

export const SUPPORT_STATUS: Record<SupportStatus, { label: string; tone: Tone }> = {
  new: { label: 'Mới', tone: 'info' },
  doing: { label: 'Đang xử lý', tone: 'success' },
  waitc: { label: 'Chờ khách phản hồi', tone: 'warning' },
  done: { label: 'Đã xử lý', tone: 'brand' }
};

/** Tab của bảng "Yêu cầu đã gửi". */
export const TICKET_TABS: ReadonlyArray<{ key: TicketFilter; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'closed', label: 'Đã xử lý' },
  { key: 'all', label: 'Tất cả' }
];

/** Liên hệ nhanh & câu hỏi thường gặp (trước ở trang Trợ giúp). Sửa câu hỏi / liên hệ tại đây. */
/** Hotline / email lấy từ cấu hình chung (shared/config/company.ts) — đổi số ở đó. */
export const CONTACTS: ReadonlyArray<{ label: string; value: string; href?: string }> = [
  { label: 'Hotline CS', value: COMPANY_CONTACTS.hotline.label, href: COMPANY_CONTACTS.hotline.href },
  { label: 'Email hỗ trợ', value: COMPANY_CONTACTS.supportEmail.label, href: COMPANY_CONTACTS.supportEmail.href },
  { label: 'Giờ làm việc', value: 'Thứ 2 – Thứ 7, 08:00 – 18:00' }
];

export const FAQ: ReadonlyArray<{ q: string; a: string }> = [
  {
    q: 'Khi nào đơn được cấp mã bill Việt An?',
    a: 'Đơn tạo xong nằm ở "Đơn nháp & chưa in". Chỉ khi bấm "In & cấp bill", hệ thống mới cấp mã bill, khóa đơn (không sửa được) và chuyển sang "Đơn hàng của tôi".'
  },
  {
    q: 'Cước được tính theo cân thực hay cân quy đổi?',
    a: 'Cân quy đổi = Dài × Rộng × Cao (cm) ÷ 5000. Cân tính cước lấy số lớn hơn giữa cân thực và quy đổi của từng kiện, nhân số lượng rồi cộng lại; làm tròn theo hãng và dịch vụ.'
  },
  {
    q: 'Chứng từ (DOC) nặng hơn 2kg thì sao?',
    a: 'Chứng từ trên 2kg được xem là hàng hóa. Hệ thống tự chuyển sang PACK, bạn cần khai Invoice đầy đủ.'
  },
  {
    q: 'Vì sao đơn bị báo phụ phí quá khổ / quá tải?',
    a: 'Mỗi hãng có giới hạn cạnh dài, tổng 3 cạnh và cân nặng mỗi kiện. Kiện vượt giới hạn sẽ bị tính phụ thu hoặc không nhận vận chuyển. Xem bảng phụ thu ở trang Giá & gợi ý dịch vụ.'
  },
  {
    q: 'Tôi có thể tạo nhiều đơn cùng lúc không?',
    a: 'Có. Dùng "Tạo đơn từ Excel" (tối đa 100 đơn/lần) hoặc kết nối API ở trang E-commerce.'
  }
];
