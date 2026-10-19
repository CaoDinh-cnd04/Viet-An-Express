/** Tài khoản con của nhân viên — GET /account/staff (người liên hệ có tên đăng nhập trong dbo.TCustomer_NguoiLienLac). */
export interface StaffAccount {
  id: number;
  userName: string;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  /** Di động 2 / email 2 — cột Di_Dong2, Email2. */
  phone2?: string | null;
  email2?: string | null;
  /** Mã quyền admin đã cấp, vd "shipments.view". */
  permissions: string[];
  active: boolean;
  createdAt: string;
}

/** Quyền admin có thể cấp — GET /account/staff/permissions. */
export interface AssignablePermission {
  code: string;
  description: string;
}

export interface StaffProfile {
  fullName: string;
  email: string | null;
  phone: string | null;
  phone2: string | null;
  email2: string | null;
  permissions: string[];
}

export interface NewStaff extends StaffProfile {
  userName: string;
  password: string;
}

export interface StaffUpdate extends StaffProfile {
  active: boolean;
}
