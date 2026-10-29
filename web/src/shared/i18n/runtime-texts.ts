/**
 * Câu tiếng Việt chỉ xuất hiện lúc chạy (dữ liệu database / hệ thống cũ / hãng vận chuyển), không có trong mã web hay backend —
 * vẫn cần bản dịch nên giữ trong từ điển. Test i18n bỏ qua các câu này khi kiểm tra khoá không dùng.
 * Thêm vào đây chỉ khi chắc chắn câu đến từ dữ liệu; câu viết trong code thì không cần.
 */
export const RUNTIME_TEXTS: ReadonlySet<string> = new Set([
  // Loại yêu cầu cũ lưu ở dbo.TContact.KeyWord (form trước 27/10/2026) — vẫn hiện ở danh sách yêu cầu đã gửi
  'Vấn đề phụ phí / cước',
  // Tên mốc hành trình hãng / hệ thống cũ trả về
  'Điểm đến',
  'Trung tâm khai thác',
  'Đã nhận hàng',
  'Kho xuất phát'
]);
