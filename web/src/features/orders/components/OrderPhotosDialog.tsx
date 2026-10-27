import { useI18n } from '@/shared/i18n';
import { Modal, Notice } from '@/shared/ui';
import type { Order } from '../types';

/**
 * Ảnh kiện chụp trên cân tại kho — căn cứ đối chiếu khối lượng & tình trạng hàng.
 * Chưa có nguồn ảnh ở backend (GET /orders/:bill/photos "Chưa làm"): chỉ báo đang phát triển, không gọi máy chủ.
 * Khi backend có ảnh: dùng useOrderPhotos (orders/api.ts) hiện lưới ảnh.
 */
export function OrderPhotosDialog({ order, onClose }: { order: Order | null; onClose: () => void }) {
  const { t } = useI18n();
  return (
    <Modal open={!!order} title={order ? t('Ảnh kiện hàng · {bill}', { bill: order.bill }) : ''} onClose={onClose}>
      <Notice tone="info">{t('Chức năng xem ảnh kiện đang được phát triển.')}</Notice>
    </Modal>
  );
}
