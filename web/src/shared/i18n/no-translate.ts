/**
 * Chữ tiếng Việt cố ý KHÔNG dịch: tên riêng, mã dữ liệu gửi backend / hệ thống cũ,
 * nội dung chỉ dùng cho tiếng Việt (vd từ khoá nhận diện chứng từ).
 */
export const NO_TRANSLATE: ReadonlySet<string> = new Set([
  'Việt An Express',
  'Việt An',
  // Dữ liệu mẫu (file CSV mẫu, ví dụ gọi API) — giữ nguyên tiếng Việt
  'Váy hoa nữ',
  'Váy',
  // Ký hiệu tiền đồng
  'đ',
  // Lỗi dành cho lập trình viên, không hiện cho khách
  'useToast phải nằm trong <ToastProvider>',
  // Tin nhắn gửi CS Việt An (CS đọc tiếng Việt)
  'Khách hàng xác nhận đã xử lý xong.',
  'Khách hàng nhắc CS xử lý yêu cầu.',
  // Chữ ghi vào nội dung yêu cầu gửi CS (support/lib/draft.ts)
  'vận đơn',
  'Mức hài lòng'
]);
