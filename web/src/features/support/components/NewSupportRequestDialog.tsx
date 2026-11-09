import { useI18n } from '@/shared/i18n';
import { Modal } from '@/shared/ui';
import type { RequestPreset } from '../lib/draft';
import type { SupportTicket } from '../types';
import { SupportRequestForm } from './SupportRequestForm';

interface NewSupportRequestDialogProps {
  open: boolean;
  preset?: RequestPreset;
  onClose: () => void;
  /** Gửi xong; không truyền thì chỉ đóng hộp. */
  onSent?: (ticket: SupportTicket) => void;
}

/** Hộp thoại gửi yêu cầu cho CS — nút "Tạo yêu cầu" ở trang Hỗ trợ & góp ý, "Báo sự cố" ở Đơn hàng của tôi. */
export function NewSupportRequestDialog({ open, preset, onClose, onSent }: NewSupportRequestDialogProps) {
  const { t } = useI18n();
  return (
    <Modal open={open} title={t('Gửi yêu cầu cho CS Việt An')} onClose={onClose} footerNote={t('CS trả lời ngay trong yêu cầu ở trang Hỗ trợ & góp ý.')}>
      <SupportRequestForm preset={preset} onCancel={onClose} onSent={ticket => (onSent ? onSent(ticket) : onClose())} />
    </Modal>
  );
}
