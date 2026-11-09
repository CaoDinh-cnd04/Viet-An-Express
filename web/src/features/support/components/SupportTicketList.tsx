import type { CSSProperties, ReactNode } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon } from '@/shared/ui';
import { SUPPORT_STATUS } from '../constants';
import { lastMessage, unansweredCount } from '../lib/history';
import { compactTime } from '../lib/requests';
import { ticketTitle } from '../lib/ticket';
import type { SupportTicket } from '../types';
import styles from './SupportTicketList.module.css';

/** Id thẻ — link ?id=TRB-… cuộn tới thẻ đó. */
export const ticketRowId = (id: string) => `support-row-${id}`;

/** Ai đang xem: khách ("Bạn", "Chờ bạn") hay CS Việt An ("Khách", "Chờ khách"). */
export type SupportViewer = 'customer' | 'cs';

interface SupportTicketListProps {
  /** Các thẻ của trang đang xem. */
  rows: readonly SupportTicket[];
  viewer?: SupportViewer;
  /** Tiêu đề khung, mặc định "Yêu cầu của bạn". */
  heading?: string;
  /** Dòng phụ dưới vấn đề (vd tên khách ở trang CS). */
  subline?: (ticket: SupportTicket) => ReactNode;
  total: number;
  /** Ô trạng thái đang chọn (nhãn + màu) — dòng nhóm đầu danh sách. */
  group: { label: string; tone: string };
  newestFirst: boolean;
  onToggleSort: () => void;
  openId: string | null;
  onOpen: (ticket: SupportTicket) => void;
  page: number;
  pageCount: number;
  pageSize: number;
  onPage: (page: number) => void;
  /** Chữ thay danh sách khi đang tải / lỗi / trống. */
  note: string | null;
}

/** Nhãn trạng thái có chấm màu — dùng ở thẻ và đầu khung chi tiết. */
export function StatusTag({ status, viewer = 'customer' }: { status: SupportTicket['status']; viewer?: SupportViewer }) {
  const { t } = useI18n();
  return (
    <span className={styles.status} data-status={status}>
      {t(status === 'waitc' ? (viewer === 'cs' ? 'Chờ khách' : 'Chờ bạn') : SUPPORT_STATUS[status].label)}
    </span>
  );
}

/** Danh sách bên trái: mỗi yêu cầu 1 thẻ (mã, trạng thái, vấn đề, tin gần nhất, vận đơn, giờ cập nhật), phân trang. */
export function SupportTicketList(props: SupportTicketListProps) {
  const { rows, viewer = 'customer', heading = 'Yêu cầu của bạn', subline, total, group, newestFirst, onToggleSort, openId, onOpen, page, pageCount, pageSize, onPage, note } = props;
  const { t } = useI18n();
  const today = new Date();
  const from = total ? (page - 1) * pageSize + 1 : 0;
  const lastPage = page >= pageCount;

  return (
    <div className={styles.list}>
      <div className={styles.head}>
        <h2>{t(heading)} <em>{total}</em></h2>
        <button type="button" className={styles.sort} onClick={onToggleSort}>
          <Icon name="sort" size={14} /> {t(newestFirst ? 'Mới nhất' : 'Cũ nhất')}
        </button>
      </div>

      {note ? (
        <p className={styles.note}>{note}</p>
      ) : (
        <div className={styles.scroll}>
          <div className={styles.group} style={{ '--group-tone': group.tone } as CSSProperties}>{t(group.label)}</div>
          <ul className={styles.items}>
            {rows.map(x => {
              const open = openId === x.id;
              const msg = lastMessage(x);
              const fresh = unansweredCount(x, viewer);
              return (
                <li key={x.id} id={ticketRowId(x.id)}>
                  <button type="button" className={cx(styles.item, open && styles.open)} aria-current={open || undefined} onClick={() => onOpen(x)}>
                    <span className={styles.line}>
                      <span className={styles.code}>
                        {x.id}
                        {fresh > 0 && <span className={styles.newDot} title={t('{n} tin mới', { n: fresh })} aria-label={t('{n} tin mới', { n: fresh })}>{fresh}</span>}
                      </span>
                      <StatusTag status={x.status} viewer={viewer} />
                    </span>
                    <span className={styles.line}>
                      <strong className={styles.title}>{t(ticketTitle(x))}</strong>
                      {x.bill && <span className={styles.bill}><Icon name="box" size={13} /> {x.bill}</span>}
                    </span>
                    {subline && <span className={styles.sub}>{subline(x)}</span>}
                    <span className={styles.preview}>{msg.from === 'cs' ? 'CS: ' : t(viewer === 'cs' ? 'Khách: ' : 'Bạn: ')}{msg.text}</span>
                    <span className={styles.line}>
                      <span className={styles.time}>{t('Cập nhật {at}', { at: compactTime(x.updatedAt ?? x.date, today) })}</span>
                      {open && <span className={styles.viewing}>{t('Đang xem')} <Icon name="arrowRight" size={12} /></span>}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
          {lastPage && <p className={styles.end}>{t('Đã hiển thị tất cả {n} yêu cầu.', { n: total })}</p>}
        </div>
      )}

      <div className={styles.foot}>
        <span>{t('{from}–{to} / {n} yêu cầu', { from, to: Math.min(page * pageSize, total), n: total })}</span>
        <span className={styles.pager}>
          <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label={t('Trang trước')}>‹</button>
          <b>{page}</b>
          <button type="button" disabled={lastPage} onClick={() => onPage(page + 1)} aria-label={t('Trang sau')}>›</button>
        </span>
      </div>
    </div>
  );
}
