import { useEffect, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, KeyValueList, Modal, StatusPill, TextAreaField } from '@/shared/ui';
import { useReplyTrouble } from '../api';
import { TROUBLE_PRIORITY, TROUBLE_STATUS } from '../constants';
import type { TroubleTicket } from '../types';
import { TroubleThread } from './TroubleThread';
import styles from './troubles.module.css';

/** Chi tiết ticket: thông tin, lịch sử trao đổi với CS, gửi thêm thông tin / nhắc CS / đóng ticket. */
export function TroubleDetailDialog({ ticket, onClose }: { ticket: TroubleTicket | null; onClose: () => void }) {
  const { t } = useI18n();
  const reply = useReplyTrouble();
  const [message, setMessage] = useState('');

  useEffect(() => setMessage(''), [ticket?.id]);
  if (!ticket) return null;

  const done = ticket.status === 'done';
  const send = (text: string, status?: TroubleTicket['status']) =>
    reply.mutate({ id: ticket.id, reply: text, status }, { onSuccess: () => setMessage('') });

  return (
    <Modal
      open
      size="lg"
      title={t('Sự cố {id}', { id: ticket.id })}
      onClose={onClose}
      footer={
        done ? (
          <Button onClick={onClose}>{t('Đóng')}</Button>
        ) : (
          <>
            <Button variant="ghost" onClick={() => send('Khách hàng xác nhận đã xử lý xong.', 'done')} disabled={reply.isPending}>{t('Đánh dấu đã xử lý')}</Button>
            <Button onClick={() => send('Khách hàng nhắc CS xử lý yêu cầu.')} disabled={reply.isPending}>{t('Nhắc CS')}</Button>
            <Button variant="primary" onClick={() => send(message.trim())} disabled={reply.isPending || !message.trim()}>{t('Gửi thông tin')}</Button>
          </>
        )
      }
    >
      <div className={styles.detail}>
        <div className={styles.pills}>
          <StatusPill tone={TROUBLE_STATUS[ticket.status].tone}>{TROUBLE_STATUS[ticket.status].label}</StatusPill>
          <StatusPill tone={TROUBLE_PRIORITY[ticket.lv].tone}>{TROUBLE_PRIORITY[ticket.lv].label}</StatusPill>
        </div>
        <KeyValueList
          items={[
            ['Mã vận đơn', <span className="mono">{ticket.bill}</span>],
            ['Người nhận', `${ticket.cnee} · ${ticket.ct}`],
            ['Loại sự cố', t(ticket.type)],
            ['Ngày gửi', ticket.date],
            ['Người yêu cầu', `${ticket.req}${ticket.contact ? ` · ${ticket.contact}` : ''}`]
          ]}
        />
        <section>
          <h4 className={styles.subTitle}>{t('Mô tả')}</h4>
          <p className={styles.desc}>{ticket.desc}</p>
        </section>
        <section>
          <h4 className={styles.subTitle}>{t('Trao đổi với CS Việt An')}</h4>
          <TroubleThread messages={ticket.messages ?? []} side="customer" />
        </section>
        {!done && (
          <TextAreaField label="Gửi thêm thông tin cho CS" rows={3} value={message} onChange={e => setMessage(e.target.value)} placeholder="Bổ sung chứng từ, thay đổi yêu cầu…" />
        )}
      </div>
    </Modal>
  );
}
