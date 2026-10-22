import type { FieldError, FieldErrors } from 'react-hook-form';
import type { CreateOrderValues } from '../schema';

/** Tên ô theo đường dẫn form — để thông báo lỗi nói rõ ô nào (kể cả ô đang nằm trong khung thu gọn). */
const LABELS: Record<string, string> = {
  'shipper.company': 'Tên công ty người gửi',
  'shipper.originalShipper': 'Tên shipper gốc',
  'shipper.contact': 'Người liên hệ người gửi',
  'shipper.tel': 'Điện thoại người gửi',
  'shipper.address': 'Địa chỉ lấy hàng',
  'shipper.email': 'Email người gửi',
  'shipper.country': 'Quốc gia gửi',
  'shipper.branch': 'Chi nhánh gửi hàng',
  'service.carrier': 'Dịch vụ',
  'service.hub': 'Hub',
  'shipment.pieces': 'Số kiện',
  'shipment.grossWeight': 'Cân nặng',
  'receiver.country': 'Nước đến',
  'receiver.city': 'Thành phố người nhận',
  'receiver.company': 'Tên công ty người nhận',
  'receiver.contact': 'Người liên hệ người nhận',
  'receiver.tel': 'Điện thoại người nhận',
  'receiver.email': 'Email người nhận',
  'receiver.addr1': 'Địa chỉ người nhận dòng 1',
  'receiver.addr2': 'Địa chỉ người nhận dòng 2',
  'receiver.addr3': 'Địa chỉ người nhận dòng 3',
  'receiver.iossNo': 'IOSS No',
  'receiver.eoriNo': 'EORI No',
  'goods.description': 'Mô tả tổng quan hàng hóa',
  'goods.multi': 'Phân loại nhóm hàng',
  'invoice.exportType': 'Hình thức xuất khẩu',
  'invoice.dutyTerms': 'Hình thức chịu thuế',
  'invoice.currency': 'Đơn vị tiền tệ'
};

const ROW_LABELS: Record<string, string> = {
  qty: 'SL', packaging: 'loại bao bì', weight: 'cân/kiện', category: 'nhóm hàng', description: 'mô tả mặt hàng',
  descEn: 'tên hàng (EN)', descVi: 'tên hàng (VN)', unit: 'đơn vị tính', price: 'đơn giá'
};

export interface FormErrorItem {
  path: string;
  label: string;
  message: string;
}

const isFieldError = (v: unknown): v is FieldError => !!v && typeof v === 'object' && 'message' in v && typeof (v as FieldError).message === 'string';

/** Tên dễ hiểu của 1 ô lỗi, vd "packages.0.weight" → "Kiện 1: cân/kiện". */
export function fieldLabel(path: string): string {
  if (LABELS[path]) return LABELS[path];
  const row = /^(packages|invoice\.items)\.(\d+)\.(\w+)$/.exec(path);
  if (row) return `${row[1] === 'packages' ? 'Kiện' : 'Mặt hàng'} ${Number(row[2]) + 1}: ${ROW_LABELS[row[3]!] ?? row[3]}`;
  if (path.startsWith('packages')) return 'Chi tiết kiện hàng';
  if (path.startsWith('invoice')) return 'Invoice';
  return path;
}

/** Trải lỗi lồng nhau của react-hook-form thành danh sách theo thứ tự trên form. */
export function listFormErrors(errors: FieldErrors<CreateOrderValues>): FormErrorItem[] {
  const out: FormErrorItem[] = [];
  const walk = (node: unknown, path: string) => {
    if (!node || typeof node !== 'object') return;
    if (isFieldError(node) && node.message) {
      const p = path.replace(/\.root$/, '');
      out.push({ path: p, label: fieldLabel(p), message: node.message });
    }
    for (const [key, child] of Object.entries(node)) {
      if (key === 'ref' || key === 'message' || key === 'type' || key === 'types') continue;
      walk(child, path ? `${path}.${key}` : key);
    }
  };
  walk(errors, '');
  return out;
}
