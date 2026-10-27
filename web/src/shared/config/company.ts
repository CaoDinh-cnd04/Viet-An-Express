/** Nhận diện và liên hệ dùng chung giữa các module của website. */
export const COMPANY = {
  name: 'Việt An Express',
  legalName: 'Viet An Express International Co., Ltd',
  foundedYear: 2010,
  copyrightFrom: 2013,
  address: 'Số 14 Sam Sơn, phường Tân Sơn Nhất, TP. Hồ Chí Minh',
  mapUrl: 'https://www.google.com/maps/search/?api=1&query=14+Sam+S%C6%A1n+T%C3%A2n+S%C6%A1n+Nh%E1%BA%A5t+H%E1%BB%93+Ch%C3%AD+Minh'
} as const;

export interface ContactLink {
  label: string;
  href: string;
}

/** Kênh liên hệ — `href` dùng trực tiếp cho thẻ <a>. */
export const CONTACTS = {
  phone: { label: '028 3948 3949', href: 'tel:+842839483949' },
  hotline: { label: '0909 805 845', href: 'tel:+84909805845' },
  email: { label: 'phuc.vo@vietanexpress.com', href: 'mailto:phuc.vo@vietanexpress.com' },
  /** Đầu mối khiếu nại / hỗ trợ khách theo Điều khoản dịch vụ (mục 13.5). */
  supportEmail: { label: 'cskh@vietanexpress.com.vn', href: 'mailto:cskh@vietanexpress.com.vn' },
  zalo: { label: 'Zalo 0909 805 845', href: 'https://zalo.me/0909805845' }
} as const satisfies Record<string, ContactLink>;

export const CARRIERS = ['DHL', 'FedEx', 'UPS', 'TNT'] as const;
