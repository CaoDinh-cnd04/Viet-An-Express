import type { Tone } from '@/shared/ui';
import type { TroublePriority, TroubleStatus } from './types';

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
