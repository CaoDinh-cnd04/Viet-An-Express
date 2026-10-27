import { CONTACTS as COMPANY_CONTACTS } from '@/shared/config/company';
import type { IconName, Tone } from '@/shared/ui';
import type { TroubleKind, TroublePriority, TroubleStatus } from './types';

/** Loại yêu cầu: khách chọn Hỗ trợ & sự cố hoặc Góp ý; "Sự cố vận đơn" chỉ còn cho yêu cầu gửi trước 27/10/2026. */
export const TROUBLE_KINDS: Record<TroubleKind, { label: string; hint: string; icon: IconName; tone: Tone }> = {
  su_co: { label: 'Sự cố vận đơn', hint: 'Giao chậm, thất lạc, hư hỏng, phụ phí… của 1 đơn hàng', icon: 'alert', tone: 'danger' },
  ho_tro: { label: 'Hỗ trợ & sự cố', hint: 'Sự cố đơn hàng, tracking, cước, tạo đơn, sửa thông tin…', icon: 'help', tone: 'info' },
  gop_y: { label: 'Góp ý', hint: 'Ý kiến để Việt An cải thiện dịch vụ và phần mềm', icon: 'message', tone: 'brand' }
};

export const TROUBLE_KIND_ORDER: readonly TroubleKind[] = ['ho_tro', 'gop_y'];

/** Chủ đề yêu cầu hỗ trợ. */
export const SUPPORT_TOPICS = ['Hỗ trợ tạo đơn', 'Tra cứu / tracking', 'Cước & phụ phí', 'Sửa thông tin người gửi', 'Báo lỗi phần mềm', 'Khác'] as const;

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

export const TROUBLE_TYPES = [
  'Giao chậm / trễ hẹn',
  'Thất lạc kiện hàng',
  'Hư hỏng / vỡ hàng',
  'Thiếu hàng trong kiện',
  'Sai thông tin người nhận',
  'Phát sai địa chỉ',
  'Vấn đề phụ phí / cước',
  'Yêu cầu giữ / đổi địa chỉ',
  'Khác'
] as const;

/** Câu gợi ý theo loại sự cố — khách bấm để thêm vào mô tả, không cần gõ. */
export const TROUBLE_SUGGESTIONS: Readonly<Record<(typeof TROUBLE_TYPES)[number], readonly string[]>> = {
  'Giao chậm / trễ hẹn': ['Tracking không cập nhật nhiều ngày', 'Người nhận chưa nhận được hàng', 'Nhờ CS kiểm tra ngày phát dự kiến', 'Nhờ CS hối hãng phát sớm'],
  'Thất lạc kiện hàng': ['Người nhận báo thiếu kiện so với số kiện gửi', 'Tracking báo đã phát nhưng người nhận không nhận được', 'Nhờ CS truy tìm kiện hàng', 'Tôi muốn làm hồ sơ bồi thường'],
  'Hư hỏng / vỡ hàng': ['Thùng hàng bị móp, rách khi nhận', 'Hàng bên trong bị vỡ', 'Tôi sẽ gửi ảnh hàng hư hỏng', 'Tôi muốn làm hồ sơ bồi thường'],
  'Thiếu hàng trong kiện': ['Người nhận báo thiếu hàng trong kiện', 'Thùng có dấu hiệu bị mở', 'Tôi sẽ gửi ảnh và danh sách hàng thiếu'],
  'Sai thông tin người nhận': ['Sai số điện thoại người nhận', 'Sai tên người nhận', 'Nhờ CS cập nhật thông tin đúng với hãng'],
  'Phát sai địa chỉ': ['Hàng được phát tới địa chỉ khác', 'Nhờ CS liên hệ hãng thu hồi và phát lại', 'Tôi sẽ gửi địa chỉ đúng'],
  'Vấn đề phụ phí / cước': ['Nhờ CS kiểm tra phụ phí vùng sâu vùng xa', 'Cân tính cước khác với cân tôi gửi', 'Tôi chưa rõ khoản phí phát sinh', 'Nhờ CS gửi chi tiết cước'],
  'Yêu cầu giữ / đổi địa chỉ': ['Nhờ CS giữ hàng tại kho hãng', 'Tôi muốn đổi địa chỉ phát', 'Tôi muốn hoàn hàng về Việt Nam'],
  Khác: ['Nhờ CS gọi lại cho tôi', 'Tôi cần tư vấn thêm']
};

export const TROUBLE_PRIORITY: Record<TroublePriority, { label: string; tone: Tone }> = {
  low: { label: 'Thường', tone: 'neutral' },
  mid: { label: 'Gấp', tone: 'warning' },
  high: { label: 'Rất gấp', tone: 'danger' }
};

export const TROUBLE_STATUS: Record<TroubleStatus, { label: string; tone: Tone }> = {
  new: { label: 'Mới', tone: 'info' },
  doing: { label: 'Đang xử lý', tone: 'success' },
  waitc: { label: 'Chờ khách phản hồi', tone: 'warning' },
  done: { label: 'Đã xử lý', tone: 'brand' }
};

export const TROUBLE_TABS: ReadonlyArray<{ key: TroubleStatus | 'all' | 'open'; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'all', label: 'Tất cả' },
  ...(Object.keys(TROUBLE_STATUS) as TroubleStatus[]).map(key => ({ key, label: TROUBLE_STATUS[key].label }))
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
