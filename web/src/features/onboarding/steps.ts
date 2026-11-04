import type { Placement, TourId } from './lib/tour';

export interface TourStep {
  /** Giá trị thuộc tính data-tour của vùng cần chỉ vào; không có = khung giữa màn hình. Vùng không có trên trang (đơn chứng từ, chưa có đơn…) thì bỏ bước đó. */
  target?: string;
  placement: Placement;
  title: string;
  body: string;
}

/** Hướng dẫn tổng quát — tự mở lần đầu khách đăng nhập. Thêm / bớt bước: sửa mảng này và gắn data-tour vào phần tử tương ứng. */
const INTRO: readonly TourStep[] = [
  {
    placement: 'center',
    title: 'Chào mừng đến Portal Việt An Express',
    body: 'Mất khoảng 1 phút để xem nhanh các chức năng chính. Bạn có thể bỏ qua và xem lại bất cứ lúc nào trong menu tài khoản (góc trên bên phải).'
  },
  {
    target: 'nav-create',
    placement: 'right',
    title: 'Tạo đơn',
    body: 'Có 3 cách: "Tạo đơn từng bước" dẫn bạn qua từng phần, "Tạo đơn 1 trang" cho người đã quen, "Tạo đơn từ Excel" để tạo nhiều đơn một lần bằng file mẫu. Thông tin người gửi được điền sẵn theo tài khoản của bạn.'
  },
  {
    target: 'nav-orders',
    placement: 'right',
    title: 'Quản lý đơn hàng',
    body: 'Đơn vừa tạo nằm ở "Đơn nháp & chưa in" — bấm In để được cấp số vận đơn. Sau đó đơn chuyển sang "Đơn hàng của tôi": theo dõi hành trình, in bill / invoice / nhãn, xuất bảng kê Excel.'
  },
  {
    target: 'nav-support',
    placement: 'right',
    title: 'Hỗ trợ',
    body: 'Gửi yêu cầu hỗ trợ, báo sự cố đơn hàng, góp ý và xem thông báo từ Việt An. CS trả lời ngay trong từng yêu cầu.'
  },
  {
    target: 'nav-account',
    placement: 'right',
    title: 'Tài khoản',
    body: 'MyTracking cá nhân: trang tra cứu mang thương hiệu của bạn để gửi người nhận. Tài khoản nhân viên: tạo đăng nhập riêng cho từng nhân viên.'
  },
  {
    target: 'terms',
    placement: 'right',
    title: 'Điều khoản dịch vụ',
    body: 'Xem điều khoản dịch vụ của Việt An Express bất cứ lúc nào. Khi tạo đơn & in bill, bạn cần đồng ý điều khoản này.'
  },
  {
    target: 'notifications',
    placement: 'bottom',
    title: 'Thông báo',
    body: 'Số màu đỏ là thông báo chưa đọc: thay đổi phụ phí, lịch nghỉ lễ, cập nhật đơn hàng.'
  },
  {
    target: 'account',
    placement: 'bottom',
    title: 'Tài khoản',
    body: 'Đổi mật khẩu, đăng xuất và "Xem hướng dẫn sử dụng" để mở lại phần giới thiệu này.'
  },
  {
    placement: 'center',
    title: 'Sẵn sàng rồi!',
    body: 'Lần đầu vào các trang Tạo đơn 1 trang, Đơn hàng của tôi, Hỗ trợ, MyTracking, Tài khoản nhân viên sẽ có hướng dẫn từng nút của trang đó; xem lại bằng nút dấu hỏi trên thanh trên cùng. Góp ý, báo lỗi: vào "Hỗ trợ & góp ý". Cần hỗ trợ ngay? Liên hệ qua Zalo 0866709826.'
  }
];

const CREATE_QUICK: readonly TourStep[] = [
  {
    placement: 'center',
    title: 'Tạo đơn 1 trang',
    body: 'Mọi phần của đơn nằm trên 1 trang, dữ liệu tự lưu trong phiên nên tải lại trang không mất. Xem nhanh từng phần bên dưới.'
  },
  {
    target: 'create-mode',
    placement: 'bottom',
    title: 'Từng bước / 1 trang',
    body: 'Chuyển qua lại giữa 2 cách tạo đơn bất cứ lúc nào, thông tin đã nhập vẫn giữ nguyên.'
  },
  {
    target: 'create-help-links',
    placement: 'bottom',
    title: 'Tra cứu & hỗ trợ',
    body: 'Link phụ phí xăng dầu, phụ phí cộng thêm, vùng sâu vùng xa của các hãng và tra cứu mã HS.'
  },
  {
    target: 'create-nav',
    placement: 'bottom',
    title: 'Các bước',
    body: 'Bấm tên bước để chuyển tới bước đó; bước còn thiếu thông tin được đánh dấu "Còn thiếu".'
  },
  {
    target: 'create-lookup',
    placement: 'right',
    title: 'Hỗ trợ khai hàng',
    body: 'Tra mã HS theo nước nhập khẩu, chọn hàng từ thư viện mặt hàng đã lưu, chép invoice từ đơn trước hoặc nhập invoice từ file Excel mẫu.'
  },
  {
    target: 'create-shipper',
    placement: 'bottom',
    title: 'Người gửi',
    body: 'Điền sẵn theo hồ sơ tài khoản. Chọn chi nhánh gửi hàng; bấm mũi tên hoặc vào khung thông tin để sửa người liên hệ, điện thoại cho đơn này. Đổi tên công ty / địa chỉ: bấm "Sửa hồ sơ người gửi" để gửi yêu cầu cho CS.'
  },
  {
    target: 'create-service',
    placement: 'bottom',
    title: 'Hàng & dịch vụ',
    body: 'Chọn dịch vụ và hub. Số tham chiếu là mã đơn hàng của bạn (không bắt buộc), dùng để tìm đơn sau này.'
  },
  {
    target: 'create-shipment',
    placement: 'bottom',
    title: 'Thông tin đơn hàng',
    body: 'Chọn chứng từ (DOC) hoặc hàng hóa (PACK), nhập số kiện và cân nặng. Chứng từ trên 2kg tự chuyển sang hàng hóa.'
  },
  {
    target: 'create-receiver',
    placement: 'bottom',
    title: 'Người nhận',
    body: 'Gõ tên công ty để chọn lại người nhận đã gửi trước đây. Nhập mã bưu chính thì thành phố, tỉnh / bang tự điền. Địa chỉ thuộc vùng sâu vùng xa sẽ có cảnh báo phụ phí.'
  },
  {
    target: 'create-address-book',
    placement: 'bottom',
    title: 'Sổ địa chỉ & làm mới',
    body: '"Sổ địa chỉ" chọn người nhận từ các đơn đã gửi. "Làm mới" xóa toàn bộ thông tin người nhận để nhập lại.'
  },
  {
    target: 'create-packages',
    placement: 'bottom',
    title: 'Chi tiết kiện hàng',
    body: 'Nhập kích thước, cân từng kiện. Cân tính cước lấy số lớn hơn giữa cân thực và D×R×C / 5000; kiện quá khổ, quá tải so với hãng sẽ được cảnh báo.'
  },
  {
    target: 'create-addons',
    placement: 'bottom',
    title: 'Tùy chọn dịch vụ',
    body: 'Chọn dịch vụ cộng thêm nếu cần: khai giá / bảo hiểm, đóng thuế hộ (DDP), dán nhãn dễ vỡ…'
  },
  {
    target: 'create-invoice',
    placement: 'bottom',
    title: 'Invoice',
    body: 'Khai từng mặt hàng để làm thủ tục hải quan: mô tả, số lượng, đơn giá, mã HS, xuất xứ.'
  },
  {
    target: 'create-actions',
    placement: 'bottom',
    title: 'Lưu nháp / Tạo đơn & in bill',
    body: '"Lưu nháp" giữ đơn để làm tiếp. "Tạo đơn & in bill" hiện điều khoản dịch vụ: đồng ý thì cấp số vận đơn và in ngay; không đồng ý thì đơn nằm ở Đơn nháp & chưa in.'
  },
  {
    target: 'create-drafts',
    placement: 'bottom',
    title: 'Đơn nháp & chưa in',
    body: 'Đơn chưa in nằm ở đây. Bấm In để được cấp số vận đơn; đơn đã in chuyển sang Đơn hàng của tôi.'
  }
];

const ORDERS: readonly TourStep[] = [
  {
    placement: 'center',
    title: 'Đơn hàng của tôi',
    body: 'Mọi đơn đã in và được cấp số vận đơn. Xem nhanh cách lọc, in chứng từ và theo dõi đơn.'
  },
  {
    target: 'orders-status',
    placement: 'bottom',
    title: 'Lọc theo trạng thái',
    body: 'Bấm ô trạng thái để lọc, chọn được nhiều ô cùng lúc. Bên cạnh là tổng số đơn, số kiện và cân của kết quả.'
  },
  {
    target: 'orders-filters',
    placement: 'bottom',
    title: 'Bộ lọc',
    body: 'Lọc theo ngày gửi, loại hàng, khoảng cân. "Xóa lọc" để về mặc định.'
  },
  {
    target: 'orders-search',
    placement: 'bottom',
    title: 'Tìm đơn',
    body: 'Chọn tìm theo trường nào, gõ từ khóa rồi bấm dấu phẩy hoặc Enter để thành thẻ. Dán cả danh sách mã từ Excel để tìm nhiều đơn một lần.'
  },
  {
    target: 'orders-bulk-print',
    placement: 'bottom',
    title: 'In nhiều bill',
    body: 'Tick ô đầu dòng để chọn đơn rồi chọn chứng từ cần in; không chọn đơn nào thì in cả trang đang xem.'
  },
  {
    target: 'orders-export',
    placement: 'bottom',
    title: 'Xuất bảng kê',
    body: 'Tải file Excel các đơn của trang đang xem hoặc toàn bộ kết quả lọc.'
  },
  {
    target: 'orders-tracking',
    placement: 'bottom',
    title: 'Hành trình',
    body: 'Link tra hành trình vận đơn trên trang Việt An và trang của hãng.'
  },
  {
    target: 'orders-print',
    placement: 'bottom',
    title: 'In chứng từ',
    body: 'In nhanh Bill A4, Invoice, CVCK của từng đơn ngay trên dòng.'
  },
  {
    target: 'orders-label',
    placement: 'bottom',
    title: 'Nhãn A6',
    body: 'In nhãn shipping mark khổ A6, mỗi kiện 1 nhãn để dán lên thùng.'
  },
  {
    target: 'orders-tools',
    placement: 'bottom',
    title: 'Thao tác khác',
    body: 'Xem chi tiết đơn, ảnh kiện hàng, copy đơn để tạo đơn mới tương tự, báo sự cố cho CS.'
  }
];

const SUPPORT: readonly TourStep[] = [
  {
    target: 'support-form',
    placement: 'bottom',
    title: 'Gửi yêu cầu mới',
    body: 'Chọn "Hỗ trợ & sự cố" (sự cố đơn hàng, tracking, cước, sửa thông tin…) hoặc "Góp ý". Nhập mã vận đơn nếu có, nội dung, đính kèm tệp nếu cần; tick "Cần xử lý gấp" khi cần CS ưu tiên.'
  },
  {
    target: 'support-contacts',
    placement: 'bottom',
    title: 'Liên hệ nhanh',
    body: 'Hotline, email, Zalo của CS Việt An khi cần trao đổi ngay.'
  },
  {
    target: 'support-tickets',
    placement: 'bottom',
    title: 'Yêu cầu đã gửi',
    body: 'Lọc yêu cầu chưa xong / đã xử lý, tìm theo mã, vận đơn, nội dung. Bấm 1 dòng để xem chi tiết; khi CS chuyển "Chờ khách phản hồi" bạn trả lời ngay trong yêu cầu đó.'
  }
];

const MYTRACKING: readonly TourStep[] = [
  {
    target: 'mt-publish',
    placement: 'bottom',
    title: 'Link tra cứu công khai',
    body: 'Sao chép link này gửi người nhận hàng để họ tra cứu trên trang mang thương hiệu của bạn. Thêm số bill sau dấu "/" cuối để mở thẳng vận đơn.'
  },
  {
    target: 'mt-brand',
    placement: 'right',
    title: 'Thông tin thương hiệu',
    body: 'Tên công ty, điện thoại, địa chỉ, website và logo hiển thị trên trang tra cứu.'
  },
  {
    target: 'mt-colors',
    placement: 'right',
    title: 'Màu thương hiệu',
    body: 'Chọn màu chủ đạo của trang tra cứu cho hợp nhận diện công ty bạn.'
  },
  {
    target: 'mt-content',
    placement: 'right',
    title: 'Nội dung chính',
    body: 'Tiêu đề và mô tả ngắn hiện trên trang, ví dụ lời chào hoặc chương trình ưu đãi.'
  },
  {
    target: 'mt-banners',
    placement: 'right',
    title: 'Hình ảnh quảng cáo',
    body: 'Tối đa 5 ảnh banner, dùng nút Lên / Xuống để sắp thứ tự.'
  },
  {
    target: 'mt-background',
    placement: 'right',
    title: 'Ảnh nền trang',
    body: 'Ảnh nền phía sau nội dung trang tra cứu; xóa ảnh để dùng nền mặc định.'
  },
  {
    target: 'mt-channels',
    placement: 'right',
    title: 'Cổng liên lạc & mạng xã hội',
    body: 'Chọn các kênh (Zalo, điện thoại, Facebook…) để người nhận liên hệ bạn từ trang tra cứu.'
  },
  {
    target: 'mt-save',
    placement: 'right',
    title: 'Lưu',
    body: 'Thay đổi chỉ hiện trên link công khai sau khi bấm Lưu. "Khôi phục mặc định" đưa trang về mẫu ban đầu.'
  },
  {
    target: 'mt-preview',
    placement: 'bottom',
    title: 'Xem trước',
    body: 'Xem trang tra cứu trên máy tính và điện thoại trước khi lưu.'
  }
];

const STAFF: readonly TourStep[] = [
  {
    placement: 'center',
    title: 'Tài khoản nhân viên',
    body: 'Mỗi nhân viên đăng nhập bằng tài khoản riêng, không cần dùng chung mật khẩu công ty. Nhân viên dùng được mọi chức năng trừ quản lý tài khoản nhân viên, MyTracking và đổi mật khẩu; chỉ thấy đơn do mình tạo, còn bạn thấy đơn của mọi nhân viên.'
  },
  {
    target: 'staff-add',
    placement: 'bottom',
    title: 'Thêm nhân viên',
    body: 'Nhập họ tên, tên đăng nhập, mật khẩu và số điện thoại / email của nhân viên.'
  },
  {
    target: 'staff-tabs',
    placement: 'bottom',
    title: 'Lọc nhân viên',
    body: 'Xem nhân viên đang hoạt động hoặc đã khóa; ô tìm bên phải tìm theo tên, tên đăng nhập, email.'
  },
  {
    target: 'staff-actions',
    placement: 'bottom',
    title: 'Thao tác',
    body: 'Sửa thông tin, đặt lại mật khẩu (nhân viên không tự đổi được), khóa / mở khóa đăng nhập. Xóa chỉ gỡ đăng nhập, đơn nhân viên đã tạo vẫn giữ nguyên.'
  }
];

export const TOURS: Readonly<Record<TourId, readonly TourStep[]>> = {
  intro: INTRO,
  'create-quick': CREATE_QUICK,
  orders: ORDERS,
  support: SUPPORT,
  mytracking: MYTRACKING,
  staff: STAFF
};
