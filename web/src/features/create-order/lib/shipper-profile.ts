import { isPhone, sanitizePhone } from '@/shared/lib/phone';
import { RULES } from '@/features/order-form';
import type { CreateOrderValues } from '@/features/order-form';

type Shipper = CreateOrderValues['shipper'];

/**
 * Hồ sơ người đang đăng nhập (GET /me). Tài khoản chính: lấy từ dbo.TCustomer.
 * Tài khoản con: công ty / địa chỉ / MST của công ty, người liên hệ / SĐT / email của nhân viên (trống nếu chưa khai).
 */
export interface CustomerProfile {
  companyName: string;
  contactName?: string | null;
  phone?: string | null;
  address?: string | null;
  taxCode?: string | null;
  email?: string | null;
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Người gửi điền sẵn từ hồ sơ khách (dbo.TCustomer): tên công ty, người liên hệ, điện thoại, địa chỉ, MST, email.
 * Khách vẫn sửa được. Chỉ điền ô đang trống (đơn nhân bản / mở lại nháp giữ nguyên dữ liệu đã có)
 * và bỏ qua dữ liệu cũ không hợp lệ (SĐT lẫn chữ, email sai) để không gây lỗi form.
 */
export function shipperFromProfile(current: Shipper, profile: CustomerProfile): Partial<Shipper> {
  const phone = sanitizePhone(profile.phone ?? '').trim();
  const email = (profile.email ?? '').trim();
  const source: Partial<Shipper> = {
    company: profile.companyName,
    contact: profile.contactName ?? '',
    tel: isPhone(phone) ? phone : '',
    address: (profile.address ?? '').trim().slice(0, RULES.shipperAddressMax),
    taxId: profile.taxCode ?? '',
    email: EMAIL.test(email) ? email : ''
  };
  const fill: Partial<Shipper> = {};
  for (const [key, value] of Object.entries(source) as Array<[keyof Shipper, string]>) {
    const v = value.trim();
    if (v && !(current[key] ?? '').trim()) fill[key] = v;
  }
  return fill;
}
