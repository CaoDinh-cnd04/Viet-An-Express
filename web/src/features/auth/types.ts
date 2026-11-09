/**
 * Loại tài khoản: tài khoản chính của khách (admin), tài khoản con của nhân viên khách,
 * hoặc nhân viên Việt An (dbo.USERS) làm thay khách — `vietan`.
 */
export type AccountType = 'customer' | 'staff' | 'vietan';

/** Khách hàng đang đăng nhập — GET /me (hồ sơ ở dbo.TCustomer). */
export interface SessionUser {
  userName?: string;
  /** Tên hiển thị: người liên hệ của khách, hoặc họ tên nhân viên với tài khoản con. */
  fullName?: string;
  accountType?: AccountType;
  /** true = tài khoản chính, quản lý được tài khoản nhân viên và MyTracking. */
  isAdmin?: boolean;
  /** Mã quyền (vd "shipments.create") — UI ẩn chức năng không có quyền. */
  permissions?: string[];
  customerCode: string;
  companyName: string;
  contactName?: string | null;
  email?: string | null;
  /** SĐT, địa chỉ, MST ở dbo.TCustomer — điền sẵn người gửi khi tạo đơn. */
  phone?: string | null;
  address?: string | null;
  taxCode?: string | null;
  avatarUrl?: string | null;
  defaultBranch?: string | null;
  /** Nhân viên Việt An đang làm thay khách (accountType = vietan): tên đăng nhập / họ tên của nhân viên. customerCode trống = chưa chọn khách. */
  agentUserName?: string | null;
  agentName?: string | null;
  /** Khách mới nhân viên Việt An vừa tạo (TCustomer.CustomerSource "Portal - Khách mới"): người gửi để trống, sửa được, lưu đơn thì ghi vào hồ sơ. */
  newCustomer?: boolean;
  /** Nhân viên Việt An là quản trị / CS — gán được nhân viên phụ trách cho khách mới. */
  canAssignManager?: boolean;
  /** Khách mới chưa được gán người phụ trách (Staff_ID còn là người tạo). */
  managerPending?: boolean;
  /** dbo.TCustomer.CustomerID của khách đang đăng nhập / làm thay (0 = nhân viên Việt An chưa chọn khách). */
  customerId?: number;
}

/**
 * Trạng thái phiên:
 * - `authenticated`: đã đăng nhập.
 * - `anonymous`: chưa đăng nhập / hết phiên (401).
 * - `open`: backend chưa bật đăng nhập (404/501) — portal vẫn mở để thử.
 */
export type Session = { status: 'authenticated'; user: SessionUser } | { status: 'anonymous' } | { status: 'open' };

/** 1 khách nhân viên Việt An được làm thay — GET /auth/vietan/customers. */
export interface ActableCustomer {
  id: number;
  code: string;
  companyName: string;
  phone?: string | null;
  address?: string | null;
  /** Tên đăng nhập của khách (TCustomer.Login_UserName). */
  loginUserName?: string | null;
  /** Khách mới tạo trên portal, CS chưa hoàn thiện ở CMS. */
  isNew?: boolean;
  /** Nhân viên phụ trách (TCustomer.Staff_ID → USERS). */
  managerId?: number | null;
  managerName?: string | null;
}

/** Nhân viên sales / CS để gán phụ trách khách mới — GET /auth/vietan/support-staff. */
export interface SupportStaff {
  id: number;
  userName: string;
  fullName: string;
}

/** Nhân viên Việt An đúng mật khẩu → đã gửi mã OTP tới email (đã che bớt); nhập mã ở bước 2. */
/** Đúng mật khẩu, chờ nhập mã OTP gửi email — nhân viên Việt An (/auth/vietan/login) và khách khi bật OTP (/auth/login, có otpRequired). */
export interface OtpChallenge {
  challengeId: string;
  email: string;
  expiresInSeconds: number;
  otpRequired?: true;
  /** Lần đầu (khách): chưa gửi mã — nhập email nhận mã + đồng ý điều khoản ở /auth/otp-email. */
  needEmail?: boolean;
  /** Email hồ sơ hiện có — điền sẵn ô email. */
  suggestedEmail?: string | null;
}

/** Ai đang nhập OTP: quyết định endpoint xác nhận mã. */
export type OtpKind = 'customer' | 'vietan';

export interface LoginRequest {
  username: string;
  password: string;
  remember: boolean;
}
