import type { StepSection } from '../components/StepIndicator';

/** Id phần tử của từng khung trong form — mục lục bấm để cuộn tới, theo dõi khung đang xem khi cuộn. */
export const secId = (key: string) => `order-sec-${key}`;

/** Bước "Kiện hàng & tùy chọn" (wizard) — quay về đây khi có kiện vượt giới hạn hãng. */
export const PACKAGES_STEP = 1;

/** Các khung con của từng bước, theo đúng thứ tự trên trang. Chứng từ (DOC) không có bảng kiện và Invoice. Hàm thuần — có test. */
export function stepSections(isPack: boolean): StepSection[][] {
  return [
    [
      { id: secId('shipper'), label: 'Người gửi' },
      { id: secId('receiver'), label: 'Người nhận' },
      { id: secId('service'), label: 'Dịch vụ' },
      { id: secId('shipment'), label: 'Thông tin đơn hàng' }
    ],
    [
      // Hàng hóa: nhóm hàng khai ngay trong "Chi tiết kiện hàng"; chứng từ: khung "Nội dung chứng từ"
      ...(isPack ? [{ id: secId('packages'), label: 'Chi tiết kiện hàng' }] : []),
      { id: secId('addons'), label: 'Tùy chọn dịch vụ' }
    ],
    ...(isPack ? [[{ id: secId('invoice'), label: 'Invoice (khai báo hải quan)' }]] : [])
  ];
}
