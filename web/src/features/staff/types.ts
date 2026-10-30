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
  active: boolean;
  createdAt: string;
}

export interface StaffProfile {
  fullName: string;
  email: string | null;
  phone: string | null;
  phone2: string | null;
  email2: string | null;
}

export interface NewStaff extends StaffProfile {
  userName: string;
  password: string;
}

export interface StaffUpdate extends StaffProfile {
  active: boolean;
}
