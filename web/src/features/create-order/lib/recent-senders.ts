import type { CreateOrderValues } from '@/features/order-form';

/** 1 người gửi đã dùng ở đơn trước — GET /orders/senders?q=. */
export interface RecentSender {
  company: string;
  contact?: string | null;
  phone?: string | null;
  address?: string | null;
  taxId?: string | null;
  email?: string | null;
  originalShipper?: string | null;
  /** 'yyyy-MM-dd' */
  lastUsed?: string | null;
}

type ShipperField = keyof Pick<CreateOrderValues['shipper'], 'company' | 'contact' | 'tel' | 'address' | 'taxId' | 'email' | 'originalShipper'>;

/** Chữ gõ ở ô tên công ty / người gửi đủ để gợi ý: 1–100 ký tự. */
export function recentSenderQuery(input: string | undefined): string | null {
  const text = (input ?? '').trim();
  return text.length >= 1 && text.length <= 100 ? text : null;
}

/** Chọn 1 người gửi cũ → điền lại các ô người gửi (ô trống ở đơn cũ thì xoá ô đó, không giữ dữ liệu của người gửi trước). */
export function senderFields(s: RecentSender, maxAddress: number): Array<[ShipperField, string]> {
  return [
    ['company', s.company],
    ['contact', s.contact ?? ''],
    ['tel', s.phone ?? ''],
    ['address', (s.address ?? '').slice(0, maxAddress)],
    ['taxId', s.taxId ?? ''],
    ['email', s.email ?? ''],
    ['originalShipper', s.originalShipper ?? '']
  ];
}

/** Dòng phụ trong danh sách: "người liên hệ · điện thoại · địa chỉ lấy hàng". */
export function recentSenderSubtitle(s: RecentSender): string {
  return [s.contact, s.phone, s.address].filter(v => v?.trim()).join(' · ');
}
