import { useI18n } from '@/shared/i18n';
import { Button, Modal } from '@/shared/ui';
import { useDeleteEcomOrders } from '../api';
import type { EcomOrder } from '../types';

/** Xác nhận xóa đơn E-commerce. Chỉ gửi đơn chưa gửi; đơn đã xác nhận gửi / có bill được báo là giữ lại. */
export function DeleteOrdersModal({ orders, onClose, onDeleted }: { orders: ReadonlyArray<EcomOrder>; onClose: () => void; onDeleted: () => void }) {
  const { t } = useI18n();
  const remove = useDeleteEcomOrders();
  const deletable = orders.filter(o => !o.bill && !o.confirmed);
  const billed = orders.length - deletable.length;
  const single = deletable.length === 1 ? deletable[0] : undefined;

  return (
    <Modal
      open={orders.length > 0}
      title="Xóa đơn E-commerce"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>{t('Hủy')}</Button>
          <Button
            variant="primary"
            disabled={!deletable.length || remove.isPending}
            onClick={() => remove.mutate(deletable.map(o => o.id), { onSuccess: () => { onDeleted(); onClose(); } })}
          >
            {t(remove.isPending ? 'Đang xóa…' : 'Xóa đơn')}
          </Button>
        </>
      }
    >
      <p>
        {single
          ? t('Xóa đơn {ref} khỏi danh sách?', { ref: single.ref })
          : t('Xóa {n} đơn khỏi danh sách?', { n: deletable.length })}
      </p>
      {billed > 0 && <p>{t('{n} đơn đã gửi sẽ được giữ lại (không xóa được).', { n: billed })}</p>}
    </Modal>
  );
}
