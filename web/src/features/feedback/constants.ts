/** 1 phương án trả lời. `exclusive`: chọn phương án này thì bỏ các phương án khác (vd "Không có"). */
export interface SurveyOption {
  label: string;
  exclusive?: boolean;
}

/**
 * 1 câu hỏi khảo sát. Nhãn câu hỏi / phương án là tiếng Việt gốc — gửi nguyên văn lên backend (lưu kèm câu trả lời),
 * giao diện dịch bằng t(). Đổi chữ của câu hỏi thì góp ý cũ vẫn giữ chữ cũ; thống kê ở trang quản trị đếm theo `id` + nhãn phương án.
 */
export interface SurveyQuestion {
  id: string;
  question: string;
  /** Chọn được nhiều phương án. */
  multiple?: boolean;
  options: SurveyOption[];
  /** Có ô ghi chú thêm (placeholder). */
  notePlaceholder?: string;
}

export interface SurveySection {
  title: string;
  questions: SurveyQuestion[];
}

/** Giới hạn khảo sát — khớp backend (Domain/Feedback.cs). */
export const SURVEY_LIMITS = { noteMax: 500 } as const;

/** Khảo sát trải nghiệm portal ở trang Góp ý — mọi câu đều không bắt buộc. */
export const SURVEY_SECTIONS: SurveySection[] = [
  {
    title: 'Tính năng',
    questions: [
      {
        id: 'feature_most',
        question: 'Tính năng nào anh/chị dùng thường xuyên nhất?',
        options: [{ label: 'Đăng nhập' }, { label: 'Tracking' }, { label: 'Tạo đơn' }, { label: 'Quản lý đơn' }]
      },
      {
        id: 'feature_missing',
        question: 'Còn tính năng nào đang thiếu khiến anh/chị phải xử lý thủ công bên ngoài hệ thống?',
        multiple: true,
        options: [
          { label: 'Không thiếu', exclusive: true },
          { label: 'Đối soát công nợ, hóa đơn' },
          { label: 'Báo cáo, thống kê chi phí' },
          { label: 'Thông báo trạng thái đơn (email, Zalo)' },
          { label: 'Sổ địa chỉ người nhận' },
          { label: 'Đồng bộ đơn từ sàn TMĐT' }
        ],
        notePlaceholder: 'Tính năng khác còn thiếu…'
      },
      {
        id: 'tracking_info',
        question: 'Kết quả tracking đã đủ thông tin cần thiết chưa? Còn thiếu gì?',
        multiple: true,
        options: [
          { label: 'Đã đủ', exclusive: true },
          { label: 'Thiếu ngày giao dự kiến' },
          { label: 'Thiếu chi tiết hành trình từng chặng' },
          { label: 'Thiếu người ký nhận, ảnh giao hàng' },
          { label: 'Cập nhật trạng thái chậm' }
        ],
        notePlaceholder: 'Thông tin khác còn thiếu…'
      },
      {
        id: 'order_validation',
        question: 'Chức năng tạo đơn có bắt hết các lỗi nhập liệu hay gặp thực tế không?',
        options: [{ label: 'Bắt được hết' }, { label: 'Bắt được phần lớn' }, { label: 'Hay lọt lỗi' }],
        notePlaceholder: 'Ví dụ lỗi từng lọt qua…'
      },
      {
        id: 'export_print',
        question: 'Xuất Excel và in nhãn A4/A6 có đúng với nhu cầu sử dụng thật không?',
        multiple: true,
        options: [
          { label: 'Đúng nhu cầu', exclusive: true },
          { label: 'Nhãn cần chỉnh bố cục, cỡ chữ' },
          { label: 'File Excel thiếu cột cần dùng' },
          { label: 'Cần thêm khổ in khác' },
          { label: 'Chưa dùng tới', exclusive: true }
        ],
        notePlaceholder: 'Cần chỉnh gì…'
      }
    ]
  },
  {
    title: 'Thao tác',
    questions: [
      {
        id: 'create_flow',
        question: 'Quy trình tạo một đơn từ lúc nhập đến lúc lưu có mượt không?',
        options: [{ label: 'Mượt, đủ bước' }, { label: 'Ổn nhưng còn bước thừa' }, { label: 'Còn thiếu bước' }, { label: 'Rườm rà, mất thời gian' }],
        notePlaceholder: 'Bước nào thừa hoặc thiếu…'
      },
      {
        id: 'search_filter',
        question: 'Tìm kiếm và lọc đơn có nhanh, có ra đúng kết quả mong muốn không?',
        options: [{ label: 'Nhanh và đúng' }, { label: 'Đúng nhưng chậm' }, { label: 'Nhanh nhưng kết quả chưa đúng ý' }, { label: 'Khó dùng' }],
        notePlaceholder: 'Ví dụ trường hợp tìm chưa ra…'
      },
      {
        id: 'repetitive',
        question: 'Có thao tác nào phải lặp đi lặp lại nhiều lần gây mất thời gian không?',
        multiple: true,
        options: [
          { label: 'Không có', exclusive: true },
          { label: 'Nhập lại thông tin người nhận' },
          { label: 'Nhập lại hàng hóa, mã HS' },
          { label: 'Chọn lại dịch vụ, hãng vận chuyển' },
          { label: 'In nhãn từng đơn một' }
        ],
        notePlaceholder: 'Thao tác khác…'
      }
    ]
  },
  {
    title: 'Giao diện',
    questions: [
      {
        id: 'overview',
        question: 'Màn hình quản lý đơn có dễ nhìn tổng quan không? (mật độ thông tin, màu sắc phân biệt trạng thái)',
        options: [{ label: 'Dễ nhìn, rõ trạng thái' }, { label: 'Quá nhiều thông tin' }, { label: 'Màu trạng thái khó phân biệt' }, { label: 'Khó nắm tổng quan' }]
      },
      {
        id: 'mobile',
        question: 'Dùng trên điện thoại hoặc máy tính bảng có ổn không?',
        options: [{ label: 'Dùng ổn' }, { label: 'Dùng được nhưng khó thao tác' }, { label: 'Không dùng được' }, { label: 'Chưa dùng thử' }],
        notePlaceholder: 'Màn hình nào khó dùng…'
      },
      {
        id: 'first_use',
        question: 'Người mới dùng lần đầu có hiểu ngay các nút bấm, bảng biểu, thông báo không?',
        options: [{ label: 'Hiểu ngay' }, { label: 'Cần hướng dẫn một chút' }, { label: 'Khó hiểu, cần hướng dẫn chi tiết' }]
      },
      {
        id: 'confusing',
        question: 'Có chỗ nào gây nhầm lẫn không? (nhãn, icon, câu chữ thông báo lỗi)',
        multiple: true,
        options: [
          { label: 'Không có', exclusive: true },
          { label: 'Nhãn, tên nút' },
          { label: 'Biểu tượng (icon)' },
          { label: 'Câu chữ thông báo lỗi' },
          { label: 'Bảng biểu' }
        ],
        notePlaceholder: 'Chỗ nào gây nhầm lẫn…'
      }
    ]
  }
];
