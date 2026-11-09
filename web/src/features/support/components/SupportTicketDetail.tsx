import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon } from '@/shared/ui';
import { useAnswerSupportTicket } from '../api';
import { SUPPORT_PRIORITY } from '../constants';
import { historyOf, unansweredCount } from '../lib/history';
import { kindOf, ticketTitle } from '../lib/ticket';
import type { SupportTicket } from '../types';
import { StatusTag, type SupportViewer } from './SupportTicketList';
import styles from './SupportTicketDetail.module.css';

const MAX_REPLY = 4000;

/** 1 ô ở hàng thông tin: nhãn mờ + giá trị đậm (+ phụ mờ); hot = tô cam kèm cờ (mức độ gấp). */
export interface DetailInfo {
  label: string;
  value: string;
  sub?: string;
  hot?: boolean;
}

interface SupportTicketDetailProps {
  ticket: SupportTicket;
  /** Ai đang xem: khách ("Bạn" / "CS Việt An") hay CS ("Khách" / "CS Việt An"). */
  viewer?: SupportViewer;
  /** Thay hàng thông tin mặc định (Vận đơn · Người nhận · Mức độ). */
  info?: readonly DetailInfo[];
  /** Thay ô trả lời ở đáy khung (trang CS: trả lời + đổi trạng thái). */
  footer?: ReactNode;
  /** Ẩn đầu khung (tiêu đề, mã, trạng thái) khi hộp thoại đã có tiêu đề riêng. */
  hideHeader?: boolean;
  /** Bấm "Tạo yêu cầu mới" ở yêu cầu đã xử lý. */
  onNew?: () => void;
}

/**
 * Chi tiết 1 yêu cầu: đầu khung, hàng thông tin (vận đơn · người nhận · mức độ), lịch sử trao đổi cũ trước mới sau kiểu chat (tin của người xem bên phải; CS xanh lá, khách xanh dương; tin mới nhất viền đậm),
 * ô nhắn tin ở đáy (chat như bình thường — 06/11/2026 khách nhắn lúc nào cũng được, không cần CS "chờ khách"), nhắn nối vào dbo.TContact.Content.
 * Tin của bên kia mà người xem chưa trả lời: viền + nhãn đỏ "Tin mới". Có tin mới / mở yêu cầu → tự cuộn xuống tin cuối.
 */
export function SupportTicketDetail({ ticket, viewer = 'customer', info: infoProp, footer, hideHeader, onNew }: SupportTicketDetailProps) {
  const { t } = useI18n();
  const kind = kindOf(ticket);
  const history = [...historyOf(ticket)].reverse();
  const fresh = unansweredCount(ticket, viewer);
  // Mở yêu cầu / có tin mới (thời gian thực) → cuộn xuống tin cuối.
  const bodyRef = useRef<HTMLDivElement>(null);
  const seen = useRef({ id: '', count: 0 });
  useLayoutEffect(() => {
    const el = bodyRef.current;
    if (!el) return;
    const same = seen.current.id === ticket.id;
    if (!same || history.length > seen.current.count) el.scrollTo({ top: el.scrollHeight, behavior: same ? 'smooth' : 'auto' });
    seen.current = { id: ticket.id, count: history.length };
  }, [ticket.id, history.length]);
  // Hàng thông tin: Vận đơn · Người nhận · Mức độ (như mẫu); yêu cầu không gắn vận đơn thì hiện người gửi.
  const info: readonly DetailInfo[] = infoProp ?? [
    ...(ticket.bill ? [{ label: 'Vận đơn', value: ticket.bill, sub: ticket.ct }] : []),
    ...(ticket.cnee ? [{ label: 'Người nhận', value: ticket.cnee }] : []),
    ...(!ticket.bill && !ticket.cnee ? [{ label: 'Người gửi', value: ticket.req || '—', sub: ticket.contact }] : []),
    ...(kind === 'gop_y' ? [{ label: 'Nội dung góp ý', value: t(ticket.type) }] : [{ label: 'Mức độ', value: t(SUPPORT_PRIORITY[ticket.lv].label), hot: ticket.lv !== 'low' }])
  ];

  return (
    <div className={styles.detail}>
      {!hideHeader && (
        <header className={styles.head}>
          <div>
            <h3>{t(ticketTitle(ticket))}</h3>
            <span className={styles.meta}>{ticket.id} · {t('Tạo lúc {at}', { at: ticket.date })}</span>
          </div>
          <StatusTag status={ticket.status} viewer={viewer} />
        </header>
      )}

      <div ref={bodyRef} className={styles.body}>
        <dl className={styles.info}>
          {info.map(x => (
            <div key={x.label}>
              <dt>{t(x.label)}</dt>
              <dd className={cx(x.hot && styles.hot)}>
                {x.hot && <Icon name="flag" size={13} />}
                <b>{x.value}</b>
                {x.sub && <span> · {x.sub}</span>}
              </dd>
            </div>
          ))}
        </dl>

        <h4 className={styles.title}>
          <Icon name="message" size={15} /> {t('Lịch sử trao đổi')} <em>({history.length})</em>
        </h4>
        {/* Kiểu chat (như Zalo): tin của người đang xem bên phải, bên kia bên trái; CS nền xanh lá, khách nền xanh dương. */}
        <ol className={styles.log}>
          {history.map((h, i) => {
            const cs = h.kind === 'cs';
            const mine = viewer === 'cs' ? cs : !cs;
            const latest = i === history.length - 1 && history.length > 1;
            const isNew = i >= history.length - fresh;
            return (
              <li key={i} className={cx(mine ? styles.mine : styles.theirs, latest && styles.latest, isNew && styles.fresh)}>
                {!mine && <span className={cx(styles.avatar, cs && styles.avatarCs)} aria-hidden>{cs ? <Icon name="headset" size={14} /> : t(viewer === 'cs' ? 'K' : 'B')}</span>}
                <div className={cx(styles.msg, cs ? styles.msgCs : styles.msgCustomer)}>
                  <div className={styles.who}>
                    <b className={cx(cs && styles.cs)}>{cs ? t('CS Việt An') : t(viewer === 'cs' ? 'Khách' : 'Bạn')}</b>
                    {isNew ? <span className={styles.newBadge}>{t('Tin mới')}</span> : latest && <span className={styles.badge}>{t('Mới nhất')}</span>}
                    <span className={styles.at}>{h.at}</span>
                  </div>
                  <p>{h.kind === 'sent' ? ticket.desc : h.text}</p>
                </div>
              </li>
            );
          })}
        </ol>
      </div>

      {footer !== undefined ? footer : <ReplyBox id={ticket.id} done={ticket.status === 'done'} onNew={onNew} />}
    </div>
  );
}

interface SupportReplyBoxProps {
  id: string;
  /** Dòng nhãn trên ô (vd "CS đang chờ bạn"); tone cam khi đang chờ người xem trả lời. */
  label?: string;
  waiting?: boolean;
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
  /** Ctrl + Enter. */
  onSubmit?: () => void;
  /** Thêm ở đầu ô (vd chọn câu trả lời mẫu của CS). */
  tools?: ReactNode;
  /** Nút bên phải chân ô. */
  actions: ReactNode;
}

/** Ô trả lời ở đáy khung chi tiết — khách và CS dùng chung. */
export function SupportReplyBox({ id, label, waiting, value, onChange, placeholder, onSubmit, tools, actions }: SupportReplyBoxProps) {
  const { t } = useI18n();
  return (
    <div className={styles.reply}>
      {(label || tools) && (
        <div className={styles.replyTop}>
          {label && (
            <label className={cx(styles.replyLabel, waiting && styles.waiting)} htmlFor={`answer-${id}`}>
              <Icon name="message" size={14} /> {t(label)}
            </label>
          )}
          {tools}
        </div>
      )}
      <textarea id={`answer-${id}`} rows={3} maxLength={MAX_REPLY} value={value} onChange={e => onChange(e.target.value)}
        onKeyDown={e => { if (onSubmit && e.key === 'Enter' && (e.ctrlKey || e.metaKey)) onSubmit(); }} placeholder={t(placeholder)} />
      <div className={styles.replyActions}>
        <span className={styles.hintText}>{onSubmit ? t('Ctrl + Enter để gửi') : ''}</span>
        <span className={styles.replyButtons}>{actions}</span>
      </div>
    </div>
  );
}

/** Dòng chữ ở đáy khung khi không trả lời được (đã xử lý, đang chờ CS…). */
export function SupportFooterNote({ children }: { children: ReactNode }) {
  return <p className={styles.closed}>{children}</p>;
}

/** Ô nhắn CS của khách — nhắn lúc nào cũng được; yêu cầu đã xử lý thì nhắn tiếp sẽ mở lại (backend chuyển "Đang xử lý"). */
function ReplyBox({ id, done, onNew }: { id: string; done: boolean; onNew?: () => void }) {
  const { t } = useI18n();
  const answer = useAnswerSupportTicket();
  const [text, setText] = useState('');
  const send = () => text.trim() && answer.mutate({ id, text }, { onSuccess: () => setText('') });
  return (
    <SupportReplyBox
      id={id}
      label={done ? 'Yêu cầu đã xử lý — nhắn tiếp sẽ mở lại' : 'Nhắn CS Việt An'}
      tools={done && onNew ? <button type="button" className={styles.link} onClick={onNew}>{t('Tạo yêu cầu mới')}</button> : undefined}
      value={text}
      onChange={setText}
      placeholder="Nhắn CS…"
      onSubmit={send}
      actions={
        <Button variant="primary" size="sm" disabled={!text.trim() || answer.isPending} onClick={send}>
          <Icon name="send" size={14} /> {t(answer.isPending ? 'Đang gửi…' : 'Gửi')}
        </Button>
      }
    />
  );
}
