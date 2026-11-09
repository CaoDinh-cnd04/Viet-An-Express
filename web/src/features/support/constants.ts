import { CONTACTS as COMPANY_CONTACTS } from '@/shared/config/company';
import type { IconName, Tone } from '@/shared/ui';
import type { StatusTile } from './components/SupportOverview';
import type { FaqCategory, FaqItem } from './lib/faq';
import type { TicketFilter, TicketKindFilter, TicketPeriod } from './lib/requests';
import type { SupportKind, SupportPriority, SupportStatus } from './types';

/** Loại yêu cầu: khách chọn Hỗ trợ & sự cố hoặc Góp ý; "Sự cố vận đơn" chỉ còn cho yêu cầu gửi trước 27/10/2026. */
export const SUPPORT_KINDS: Record<SupportKind, { label: string; hint: string; icon: IconName; tone: Tone }> = {
  su_co: { label: 'Sự cố vận đơn', hint: 'Giao chậm, thất lạc, hư hỏng, phụ phí… của 1 đơn hàng', icon: 'alert', tone: 'danger' },
  ho_tro: { label: 'Hỗ trợ & sự cố', hint: 'Sự cố đơn hàng, tracking, cước, tạo đơn, sửa thông tin…', icon: 'help', tone: 'info' },
  gop_y: { label: 'Góp ý', hint: 'Dịch vụ, nhân viên, phần mềm', icon: 'message', tone: 'brand' }
};

export const SUPPORT_KIND_ORDER: readonly SupportKind[] = ['ho_tro', 'gop_y'];

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

/** Ô trạng thái đầu trang: icon trong ô màu nhạt + nhãn + số; tone = biến màu trong tokens.css. */
export const TICKET_TILES: ReadonlyArray<StatusTile<TicketFilter>> = [
  { key: 'waitc', label: 'Cần bạn trả lời', tone: 'var(--color-warning)', icon: 'message' },
  { key: 'doing', label: 'Đang xử lý', tone: 'var(--color-status-fly)', icon: 'clock' },
  { key: 'closed', label: 'Đã xử lý', tone: 'var(--color-success)', icon: 'checkCircle' },
  { key: 'all', label: 'Tất cả yêu cầu', tone: 'var(--color-text-muted)', icon: 'inbox' }
];

/** Ô lọc thời gian (theo lần cập nhật gần nhất). */
export const TICKET_PERIODS: ReadonlyArray<{ value: TicketPeriod; label: string }> = [
  { value: '', label: 'Tất cả' },
  { value: '7d', label: '7 ngày' },
  { value: '30d', label: '30 ngày' },
  { value: 'month', label: 'Tháng này' }
];

export const TICKET_KIND_FILTERS: ReadonlyArray<{ value: TicketKindFilter; label: string }> = [
  { value: '', label: 'Tất cả' },
  { value: 'ho_tro', label: 'Hỗ trợ & sự cố' },
  { value: 'gop_y', label: 'Góp ý' }
];

/** Số yêu cầu mỗi trang ở danh sách bên trái. */
export const TICKET_PAGE_SIZE = 10;

/** Hotline / email lấy từ cấu hình chung (shared/config/company.ts) — đổi số ở đó. */
export const CONTACTS: ReadonlyArray<{ label: string; value: string; href?: string }> = [
  { label: 'Hotline CS', value: COMPANY_CONTACTS.hotline.label, href: COMPANY_CONTACTS.hotline.href },
  { label: 'Email hỗ trợ', value: COMPANY_CONTACTS.supportEmail.label, href: COMPANY_CONTACTS.supportEmail.href },
  { label: 'Giờ làm việc', value: 'Thứ 2 – Thứ 7, 08:00 – 18:00' }
];

export const FAQ_CATEGORIES: ReadonlyArray<{ value: FaqCategory | ''; label: string }> = [
  { value: '', label: 'Tất cả' },
  { value: 'don', label: 'Tạo đơn' },
  { value: 'cuoc', label: 'Cước' },
  { value: 'gui', label: 'Giao hàng' },
  { value: 'tk', label: 'Tài khoản' }
];

/** Câu hỏi thường gặp — sửa câu hỏi / câu trả lời tại đây (CS duyệt nội dung). */
export const FAQ: readonly FaqItem[] = [
  {
    cat: 'don',
    q: 'Khi nào đơn của tôi được cấp mã bill?',
    a: 'Sau khi tạo, đơn sẽ được lưu ở mục Đơn nháp & chưa in để bạn kiểm tra lại. Khi mọi thông tin đã đúng, bạn bấm "In & cấp bill" — hệ thống sẽ cấp mã bill và chuyển đơn sang Đơn hàng của tôi.',
    link: { to: '/drafts', label: 'Mở Đơn nháp & chưa in' }
  },
  {
    cat: 'don',
    q: 'Tôi có thể sửa đơn sau khi đã in bill không?',
    a: 'Để đảm bảo thông tin khớp với hãng vận chuyển, đơn đã cấp bill sẽ được khóa lại. Nếu cần điều chỉnh, bạn chỉ cần gửi yêu cầu và chọn vấn đề "Sai thông tin người nhận" — CS Việt An sẽ hỗ trợ cập nhật giúp bạn.'
  },
  {
    cat: 'don',
    q: 'Tôi muốn tạo nhiều đơn cùng lúc thì làm thế nào?',
    a: 'Bạn có thể dùng chức năng Tạo đơn từ Excel: tải file mẫu, điền thông tin rồi tải lên, mỗi lần tối đa 100 đơn. Hệ thống sẽ kiểm tra từng dòng và báo rõ dòng nào cần sửa.',
    link: { to: '/orders/import', label: 'Mở Tạo đơn từ Excel' }
  },
  {
    cat: 'don',
    q: 'Gửi chứng từ (DOC) nặng hơn 2kg thì sao?',
    a: 'Theo quy định của hãng, chứng từ trên 2kg sẽ được tính là hàng hóa (PACK). Hệ thống sẽ tự chuyển giúp bạn, bạn chỉ cần khai thêm Invoice để hàng thông quan thuận lợi.'
  },
  {
    cat: 'cuoc',
    q: 'Cước được tính theo cân thực hay cân quy đổi?',
    a: 'Mỗi kiện sẽ được so sánh giữa cân thực và cân quy đổi (Dài × Rộng × Cao (cm) ÷ 5000), và lấy số lớn hơn để tính cước. Vì vậy, bạn nên đóng gói gọn để tiết kiệm chi phí nhé.'
  },
  {
    cat: 'cuoc',
    q: 'Vì sao đơn của tôi có phụ phí quá khổ / quá tải?',
    a: 'Mỗi hãng có giới hạn riêng về chiều dài, tổng 3 cạnh và cân nặng của từng kiện. Khi kiện vượt các mức này, hãng sẽ tính thêm phụ phí. Bạn có thể xem chi tiết tại trang Giá & gợi ý dịch vụ.',
    link: { to: '/pricing', label: 'Mở Giá & gợi ý dịch vụ' }
  },
  {
    cat: 'cuoc',
    q: 'Phụ phí vùng sâu vùng xa là gì?',
    a: 'Một số khu vực ở xa trung tâm, hãng cần thêm thời gian và chi phí để giao hàng nên sẽ thu thêm phí. Bạn có thể gửi yêu cầu kèm mã bưu chính để CS kiểm tra trước giúp bạn.'
  },
  {
    cat: 'gui',
    q: 'Hành trình đơn hàng chưa được cập nhật?',
    a: 'Thông tin hành trình được cập nhật theo dữ liệu của hãng vận chuyển nên đôi khi sẽ có độ trễ nhất định. Nếu đơn đã quá ngày dự kiến mà vẫn chưa có thông tin mới, bạn gửi yêu cầu "Giao chậm / trễ hẹn" để CS kiểm tra giúp nhé.'
  },
  {
    cat: 'gui',
    q: 'Người nhận báo chưa nhận được hàng?',
    a: 'Bạn vui lòng xem trạng thái đơn ở trang Tra cứu trước. Nếu hệ thống báo đã giao nhưng người nhận chưa nhận được, bạn gửi yêu cầu kèm mã vận đơn — CS sẽ liên hệ hãng xác minh ngay.',
    link: { to: '/tracking', label: 'Mở Tra cứu' }
  },
  {
    cat: 'tk',
    q: 'Tôi muốn thay đổi tên hoặc địa chỉ người gửi?',
    a: 'Bạn gửi yêu cầu và chọn vấn đề "Sửa thông tin người gửi", ghi rõ thông tin mới. CS Việt An sẽ cập nhật giúp bạn trong giờ làm việc.'
  },
  {
    cat: 'tk',
    q: 'Làm sao để nhân viên công ty tôi cùng tạo đơn?',
    a: 'Bạn vào Tài khoản › Tài khoản nhân viên để tạo tài khoản riêng cho từng người. Mỗi tài khoản con sẽ quản lý các đơn do mình tạo, còn tài khoản chính xem được toàn bộ.',
    link: { to: '/account/staff', label: 'Mở Tài khoản nhân viên' }
  }
];

/** Vấn đề khách chọn khi gửi → nhóm câu hỏi gợi ý trong form (không có = không gợi ý). */
export const TOPIC_FAQ: Readonly<Record<string, FaqCategory>> = {
  'Giao chậm / trễ hẹn': 'gui',
  'Thất lạc kiện hàng': 'gui',
  'Phát sai địa chỉ': 'gui',
  'Tra cứu / tracking': 'gui',
  'Sai thông tin người nhận': 'don',
  'Yêu cầu giữ / đổi địa chỉ': 'don',
  'Hỗ trợ tạo đơn': 'don',
  'Cước & phụ phí': 'cuoc',
  'Sửa thông tin người gửi': 'tk'
};
