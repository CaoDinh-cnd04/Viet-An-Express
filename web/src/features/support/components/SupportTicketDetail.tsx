import { useI18n } from '@/shared/i18n';
import { CONTACTS, SUPPORT_PRIORITY } from '../constants';
import { canAnswer } from '../lib/history';
import { kindOf } from '../lib/ticket';
import type { SupportTicket } from '../types';
import { SupportAnswerForm } from './SupportAnswerForm';
import { SupportThreadList } from './SupportThreadList';
import styles from './SupportTicketDetail.module.css';

/**
 * Chi tiết 1 yêu cầu: bên trái thông tin + nội dung khách gửi, bên phải trao đổi với CS (mới nhất trước).
 * CS chuyển "Chờ khách phản hồi" thì khách trả lời ngay tại đây; trao đổi nối vào dbo.TContact (không có bảng riêng).
 */
export function SupportTicketDetail({ ticket }: { ticket: SupportTicket }) {
  const { t } = useI18n();
  const kind = kindOf(ticket);
  const info: Array<[string, string]> = [
    ...(ticket.bill ? [['Vận đơn', [ticket.bill, ticket.cnee, ticket.ct].filter(Boolean).join(' · ')] as [string, string]] : []),
    [kind === 'gop_y' ? 'Nội dung góp ý' : 'Vấn đề', t(ticket.type)],
    ...(ticket.subject && ticket.subject !== ticket.type ? [['Tiêu đề', ticket.subject] as [string, string]] : []),
    ...(kind !== 'gop_y' && ticket.lv !== 'low' ? [['Mức độ', t(SUPPORT_PRIORITY[ticket.lv].label)] as [string, string]] : []),
    ['Người gửi', [ticket.req, ticket.contact].filter(Boolean).join(' · ')],
    ['Gửi lúc', ticket.date]
  ];

  return (
    <div className={styles.detail}>
      <div>
        <dl className={styles.kv}>
          {info.map(([label, value]) => (
            <div key={label}>
              <dt>{t(label)}</dt>
              <dd className={label === 'Vận đơn' ? 'mono' : undefined}>{value}</dd>
            </div>
          ))}
        </dl>
        <p className={styles.content}>{ticket.desc}</p>
      </div>

      <div>
        <h4 className={styles.title}>{t('Trao đổi với CS')}</h4>
        {canAnswer(ticket) && <SupportAnswerForm id={ticket.id} />}
        <SupportThreadList ticket={ticket} viewer="customer" />
        <p className={styles.hint}>
          {t('Cần gấp: gọi')} <a href={CONTACTS[0]!.href}>{CONTACTS[0]!.value}</a>
        </p>
      </div>
    </div>
  );
}
