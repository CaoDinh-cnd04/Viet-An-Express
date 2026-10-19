import type { ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, KeyValueList, Modal, StatusPill } from '@/shared/ui';
import { TROUBLE_KINDS, TROUBLE_PRIORITY, TROUBLE_STATUS } from '../constants';
import { kindOf, ticketTitle } from '../lib/chat';
import type { TroubleTicket } from '../types';
import styles from './troubles.module.css';

/** Chi tiết yêu cầu (sự cố / hỗ trợ / góp ý): thông tin, nội dung, trạng thái — CS xử lý và báo kết quả qua Thông báo. */
export function TroubleDetailDialog({ ticket, onClose }: { ticket: TroubleTicket | null; onClose: () => void }) {
  const { t } = useI18n();
  if (!ticket) return null;
  const kind = kindOf(ticket);
  const info: Array<[string, ReactNode]> = [
    ...(kind === 'su_co'
      ? ([
          ['Mã vận đơn', <span className="mono">{ticket.bill}</span>],
          ['Người nhận', `${ticket.cnee} · ${ticket.ct}`],
          ['Loại sự cố', t(ticket.type)]
        ] satisfies Array<[string, ReactNode]>)
      : [['Tiêu đề', t(ticketTitle(ticket))] as [string, ReactNode], ...(kind === 'ho_tro' ? [['Chủ đề', t(ticket.type)] as [string, ReactNode]] : [])]),
    ['Ngày gửi', ticket.date],
    ['Người yêu cầu', `${ticket.req}${ticket.contact ? ` · ${ticket.contact}` : ''}`]
  ];

  return (
    <Modal
      open
      size="lg"
      title={`${t(TROUBLE_KINDS[kind].label)} ${ticket.id}`}
      onClose={onClose}
      footerNote={t('CS Việt An báo kết quả qua Thông báo, điện thoại hoặc email')}
      footer={<Button onClick={onClose}>{t('Đóng')}</Button>}
    >
      <div className={styles.detail}>
        <div className={styles.pills}>
          <StatusPill tone={TROUBLE_STATUS[ticket.status].tone}>{TROUBLE_STATUS[ticket.status].label}</StatusPill>
          {kind === 'su_co' && <StatusPill tone={TROUBLE_PRIORITY[ticket.lv].tone}>{TROUBLE_PRIORITY[ticket.lv].label}</StatusPill>}
        </div>
        <KeyValueList items={info} />
        <section>
          <h4 className={styles.subTitle}>{t('Nội dung')}</h4>
          <p className={styles.desc}>{ticket.desc}</p>
        </section>
      </div>
    </Modal>
  );
}
