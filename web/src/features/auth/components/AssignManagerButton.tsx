import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button } from '@/shared/ui';
import { useSession } from '../api';
import { AssignManagerDialog } from './AssignManagerDialog';

/**
 * Nút "Gán nhân viên" (đã gán: "Đổi nhân viên") cho khách đang làm thay — chỉ hiện khi đó là khách mới và nhân viên Việt An
 * là quản trị / CS. Đặt ở góc dưới khung người gửi của trang tạo đơn; ngoài các trường hợp trên không hiện gì.
 */
export function AssignManagerButton() {
  const { t } = useI18n();
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  const [open, setOpen] = useState(false);
  if (!user?.newCustomer || !user.canAssignManager || !user.customerId) return null;

  return (
    <>
      <Button size="sm" onClick={() => setOpen(true)}>{t(user.managerPending ? 'Gán nhân viên' : 'Đổi nhân viên')}</Button>
      {/* Phiên không có mã người phụ trách: đổi nhân viên thì chọn lại (danh sách ở trang chọn khách có chọn sẵn). */}
      <AssignManagerDialog
        customer={open ? { id: user.customerId, companyName: user.companyName, managerId: null } : null}
        reassign={!user.managerPending}
        onClose={() => setOpen(false)}
      />
    </>
  );
}
