/** Nội dung tĩnh trang Trợ giúp. Sửa câu hỏi / liên hệ tại đây. */

export const FEEDBACK_CATEGORIES = ['Hỗ trợ tạo đơn', 'Tra cứu / tracking', 'Cước & phụ phí', 'Góp ý phần mềm', 'Báo lỗi phần mềm', 'Khác'] as const;

// TODO(CS): thay hotline / email bằng thông tin thật của Việt An trước khi go-live.
export const CONTACTS: ReadonlyArray<{ label: string; value: string }> = [
  { label: 'Hotline CS', value: '1900 0000' },
  { label: 'Email hỗ trợ', value: 'cs@vietanexpress.com.vn' },
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
