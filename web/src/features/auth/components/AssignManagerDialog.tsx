import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, Modal, Notice, SelectField } from '@/shared/ui';
import { useAssignManager, useSupportStaff } from '../api';
import type { ActableCustomer } from '../types';

interface Props {
  customer: Pick<ActableCustomer, 'id' | 'companyName' | 'managerId'> | null;
  /** Đã có người phụ trách (tiêu đề "Đổi…") — mặc định theo `customer.managerId`. */
  reassign?: boolean;
  onClose: () => void;
}

/** Quản trị / CS gán / đổi nhân viên phụ trách (sales hoặc CS) cho khách mới — ghi TCustomer.Staff_ID. Mở từ trang chọn khách và thanh đang làm thay. */
export function AssignManagerDialog({ customer, reassign, onClose }: Props) {
  const { t } = useI18n();
  const staff = useSupportStaff(customer !== null);
  const assign = useAssignManager();
  const [picked, setPicked] = useState('');
  const value = picked || (customer?.managerId ? String(customer.managerId) : '');
  const options = (staff.data ?? []).map(s => ({ value: String(s.id), label: s.fullName === s.userName ? s.fullName : `${s.fullName} (${s.userName})` }));

  const close = () => {
    setPicked('');
    onClose();
  };
  const save = () => {
    if (!customer || !value) return;
    assign.mutate({ customerId: customer.id, staffId: Number(value) }, { onSuccess: close });
  };

  return (
    <Modal
      open={customer !== null}
      title={(reassign ?? !!customer?.managerId) ? 'Đổi nhân viên phụ trách' : 'Gán nhân viên phụ trách'}
      onClose={close}
      footer={
        <>
          <Button onClick={close}>{t('Hủy')}</Button>
          <Button variant="primary" disabled={!value || assign.isPending} onClick={save}>{t('Lưu')}</Button>
        </>
      }
    >
      <p>{t('Khách')}: <strong>{customer?.companyName || t('Khách mới (chưa có tên)')}</strong></p>
      {staff.isError ? (
        <Notice tone="danger">{t('Không tải được danh sách nhân viên')}</Notice>
      ) : (
        <SelectField
          label="Nhân viên phụ trách (sales / CS)"
          required
          options={options}
          placeholder={staff.isLoading ? 'Đang tải…' : 'Chọn nhân viên'}
          value={value}
          onChange={e => setPicked(e.target.value)}
        />
      )}
    </Modal>
  );
}
