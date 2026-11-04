import type { StepSection } from '../components/StepIndicator';

/** Id phần tử của từng khung trong form — mục lục bấm để cuộn tới, theo dõi khung đang xem khi cuộn. */
export const secId = (key: string) => `order-sec-${key}`;

/** Bước "Kiện hàng & tùy chọn" (wizard) — quay về đây khi có kiện vượt giới hạn hãng. */
export const PACKAGES_STEP = 1;

/**
 * Các khung con của từng bước, theo thứ tự đọc trên trang (bước đầu: cột trái người gửi → dịch vụ → thông tin đơn hàng,
 * rồi cột phải người nhận). Chứng từ (DOC) không có bảng kiện và Invoice. Hàm thuần — có test.
 */
export function stepSections(isPack: boolean): StepSection[][] {
  return [
    [
      { id: secId('shipper'), label: 'Người gửi' },
      { id: secId('service'), label: 'Dịch vụ' },
      { id: secId('shipment'), label: 'Thông tin đơn hàng' },
      { id: secId('receiver'), label: 'Người nhận' }
    ],
    [
      // Hàng hóa: nhóm hàng khai ngay trong "Chi tiết kiện hàng"; chứng từ: khung "Nội dung chứng từ"
      ...(isPack ? [{ id: secId('packages'), label: 'Chi tiết kiện hàng' }] : []),
      { id: secId('addons'), label: 'Tùy chọn dịch vụ' }
    ],
    ...(isPack ? [[{ id: secId('invoice'), label: 'Invoice (khai báo hải quan)' }]] : [])
  ];
}
