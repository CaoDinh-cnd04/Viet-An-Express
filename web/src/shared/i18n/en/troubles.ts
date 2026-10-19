/** Tiếng Anh — Hỗ trợ & sự cố (gộp sự cố vận đơn, yêu cầu hỗ trợ, góp ý) và tab tương ứng ở trang quản trị. */
export const troubles: Record<string, string> = {
  // Trang khách
  'Báo sự cố vận đơn, gửi yêu cầu hỗ trợ hoặc góp ý — trao đổi trực tiếp với CS Việt An.':
    'Report a shipment issue, send a support request or feedback — chat directly with Viet An CS.',
  'Liên hệ & câu hỏi thường gặp': 'Contacts & FAQ',
  'Gửi yêu cầu mới': 'New request',
  'Danh sách yêu cầu': 'Request list',
  'Tìm mã yêu cầu, vận đơn, tiêu đề…': 'Search request ID, waybill, subject…',
  'Tìm yêu cầu': 'Search requests',
  'Chức năng hỗ trợ & sự cố đang được kết nối máy chủ. Cần báo gấp, vui lòng gọi hotline Việt An.':
    'Support & issues is being connected to the server. For urgent matters, please call the Viet An hotline.',
  'Không tải được danh sách yêu cầu': 'Could not load requests',
  'Không có yêu cầu nào khớp tìm kiếm.': 'No requests match your search.',
  'Không có yêu cầu nào.': 'No requests.',
  'Chọn 1 yêu cầu để xem trao đổi với CS Việt An.': 'Select a request to see the conversation with Viet An CS.',
  'Chưa có yêu cầu nào. Bấm "Gửi yêu cầu mới" khi cần báo sự cố, hỗ trợ hoặc góp ý.':
    'No requests yet. Click "New request" to report an issue, ask for support or send feedback.',
  'Bạn gửi yêu cầu lúc {date}': 'You sent this request at {date}',
  'Yêu cầu đã xử lý xong. Cần hỗ trợ thêm, vui lòng gửi yêu cầu mới.': 'This request is resolved. Need more help? Please send a new request.',
  'Đính kèm': 'Attachment',

  // Hộp gửi yêu cầu
  'Gửi yêu cầu cho CS Việt An': 'Send a request to Viet An CS',
  'Bạn cần gửi gì cho Việt An?': 'What would you like to send to Viet An?',
  'Yêu cầu hỗ trợ': 'Support request',
  'Hỏi về tạo đơn, tracking, cước, phần mềm…': 'Questions about orders, tracking, rates, the software…',
  'Ý kiến để Việt An cải thiện dịch vụ và phần mềm': 'Ideas to help Viet An improve its service and software',
  'Bạn cần hỗ trợ về vấn đề gì?': 'What do you need help with?',
  'Bạn cho CS biết tiêu đề và nội dung cần hỗ trợ nhé.': 'Please give CS a subject and describe what you need.',
  'Bạn muốn góp ý điều gì cho Việt An?': 'What feedback would you like to give Viet An?',
  'Tiêu đề, vd: Không in được nhãn': 'Subject, e.g. Cannot print labels',
  'Mô tả chi tiết điều bạn cần hỗ trợ…': 'Describe in detail what you need help with…',
  'Điều bạn thấy chưa tiện, ý tưởng cải thiện…': 'What is inconvenient, ideas for improvement…',
  'Nội dung tối thiểu 10 ký tự để CS xử lý nhanh': 'Please enter at least 10 characters so CS can help quickly',
  'Để lại liên hệ nếu bạn muốn CS phản hồi (không bắt buộc), kèm ảnh nếu có rồi bấm Gửi.':
    'Leave your contact if you would like a reply (optional), attach an image if any, then click Send.',
  'CS sẽ liên hệ ai khi có kết quả? Kiểm tra lại giúp mình, kèm ảnh / chứng từ nếu có rồi bấm Gửi.':
    'Who should CS contact with the result? Please check, attach images / documents if any, then click Send.',
  'Đính kèm ảnh / chứng từ (tùy chọn, tối đa {mb}MB)': 'Attach an image / document (optional, max {mb}MB)',

  // Trang quản trị
  'Hỗ trợ & sự cố của khách và thông báo gửi khách': 'Customer support & issues and notices to customers',
  'Yêu cầu của khách ({n})': 'Customer requests ({n})',
  'Lọc theo loại yêu cầu': 'Filter by request type',
  'Mọi loại': 'All types',
  'Loại yêu cầu': 'Request type',
  'Không có yêu cầu khớp bộ lọc.': 'No requests match the filter.',
  'Sự cố, yêu cầu hỗ trợ và góp ý của khách sẽ hiện ở đây.': 'Customer issues, support requests and feedback will appear here.',

  // Không còn trao đổi qua lại; sửa thông tin người gửi là yêu cầu hỗ trợ
  'Chi tiết yêu cầu': 'Request details',
  'Chọn 1 yêu cầu để xem chi tiết và trạng thái.': 'Select a request to see its details and status.',
  'Cập nhật {date}: {status}': 'Updated {date}: {status}',
  'CS Việt An đang xử lý và sẽ báo kết quả qua Thông báo, điện thoại hoặc email. Cần bổ sung thông tin, vui lòng gửi yêu cầu mới.':
    'Viet An CS is working on it and will report back via Notifications, phone or email. To add information, please send a new request.',
  'CS Việt An báo kết quả qua Thông báo, điện thoại hoặc email': 'Viet An CS reports back via Notifications, phone or email',
  'Đính kèm ảnh / chứng từ (tùy chọn, tối đa {mb}MB — gửi kèm email cho CS)': 'Attach an image / document (optional, max {mb}MB — emailed to CS)',
  'Sửa thông tin người gửi': 'Update sender details',
  'Sửa hồ sơ người gửi': 'Update sender profile',
  'Không tải được đơn gốc để copy': 'Could not load the original order to copy',
  'Không có yêu cầu': 'No requests',
  'Ghi chú gửi khách (không bắt buộc)': 'Note to the customer (optional)',
  'Cập nhật & báo khách': 'Update & notify customer',
  'Khách nhận thông báo riêng trên portal kèm ghi chú; xử lý chi tiết ở phần mềm cũ': 'The customer gets a private portal notice with the note; detailed handling happens in the legacy software',

  // Thiết kế lại trang Hỗ trợ & sự cố
  'Báo sự cố vận đơn, gửi yêu cầu hỗ trợ hoặc góp ý — CS Việt An xử lý và báo kết quả qua Thông báo.':
    'Report a shipment issue, send a support request or feedback — Viet An CS handles it and reports back via Notifications.',
  '{n} yêu cầu đang chờ bạn phản hồi — CS sẽ liên hệ qua SĐT / email bạn để lại.': '{n} requests are waiting for your reply — CS will contact you by the phone / email you left.',
  'Chưa có yêu cầu nào': 'No requests yet',
  'Chọn 1 loại yêu cầu ở trên khi đơn hàng gặp sự cố, cần hỗ trợ hoặc muốn góp ý cho Việt An.':
    'Pick a request type above when an order has an issue, you need help, or want to give Viet An feedback.',

  // Trang Hỗ trợ & sự cố kiểu menu + form trên trang
  'Gửi yêu cầu cho CS Việt An và theo dõi kết quả.': 'Send requests to Viet An CS and follow the results.',
  'Yêu cầu đã gửi': 'Sent requests',
  'Không tìm thấy yêu cầu': 'Request not found',
  'Không có yêu cầu nào khớp.': 'No matching requests.',
  'Chưa có yêu cầu nào. Chọn mục ở "Gửi yêu cầu" bên trái khi cần hỗ trợ.': 'No requests yet. Pick an item under "Send a request" on the left when you need help.',
  'Mã': 'ID',
  'Vận đơn / chủ đề': 'Waybill / topic',
  'Vấn đề': 'Issue',
  'VD: Sửa địa chỉ người gửi': 'E.g. Update sender address',
  'Chuyện gì xảy ra, bạn muốn xử lý thế nào': 'What happened and how would you like it handled',
  'Ảnh / chứng từ, tối đa {mb}MB — gửi kèm email cho CS': 'Image / document, max {mb}MB — emailed to CS',
  'SĐT / email': 'Phone / email',
  'Không bắt buộc': 'Optional',
  'Chọn vấn đề của đơn': 'Choose the order issue',
  'Chọn chủ đề': 'Choose a topic',
  'Chưa có yêu cầu nào. Bấm "Báo sự cố đơn hàng", "Yêu cầu hỗ trợ" hoặc "Góp ý" phía trên khi cần.': 'No requests yet. Use "Report an order issue", "Support request" or "Feedback" above when needed.',
  'CS báo kết quả qua mục Thông báo, điện thoại hoặc email.': 'CS reports results via Notifications, phone or email.',
};
