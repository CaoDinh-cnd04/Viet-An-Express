import { useI18n } from '@/shared/i18n';
import { Button, Modal, StatusPill } from '@/shared/ui';
import { SUPPORT_KINDS, SUPPORT_STATUS } from '../constants';
import { kindOf } from '../lib/ticket';
import type { SupportTicket } from '../types';
import { SupportTicketDetail } from './SupportTicketDetail';

/** Hộp chi tiết 1 yêu cầu (mở từ Đơn hàng của tôi) — cùng nội dung với dòng mở rộng ở trang Hỗ trợ & góp ý. */
export function SupportTicketDialog({ ticket, onClose }: { ticket: SupportTicket | null; onClose: () => void }) {
  const { t } = useI18n();
  if (!ticket) return null;
  return (
    <Modal
      open
      size="lg"
      title={`${t(SUPPORT_KINDS[kindOf(ticket)].label)} ${ticket.id}`}
      onClose={onClose}
      footerNote={<StatusPill tone={SUPPORT_STATUS[ticket.status].tone}>{SUPPORT_STATUS[ticket.status].label}</StatusPill>}
      footer={<Button onClick={onClose}>{t('Đóng')}</Button>}
    >
      <SupportTicketDetail ticket={ticket} />
    </Modal>
  );
}
