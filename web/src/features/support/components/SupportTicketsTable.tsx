import { Fragment } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon, StatusPill } from '@/shared/ui';
import { SUPPORT_KIND_SHORT, SUPPORT_PRIORITY, SUPPORT_STATUS, TICKET_TABS } from '../constants';
import { compactTime, type TicketFilter } from '../lib/requests';
import { kindOf } from '../lib/ticket';
import type { SupportTicket } from '../types';
import { SupportTicketDetail } from './SupportTicketDetail';
import styles from './SupportTicketsTable.module.css';

/** Id dòng trong bảng — link ?id=TRB-… cuộn tới dòng đó. */
export const ticketRowId = (id: string) => `support-row-${id}`;

interface SupportTicketsTableProps {
  rows: readonly SupportTicket[];
  counts: Record<TicketFilter, number>;
  filter: TicketFilter;
  onFilter: (f: TicketFilter) => void;
  query: string;
  onQuery: (q: string) => void;
  openId: string | null;
  onToggle: (ticket: SupportTicket) => void;
  /** Chữ thay bảng khi đang tải / lỗi / trống. */
  note: string | null;
}

/** Bảng "Yêu cầu đã gửi": tab lọc + ô tìm; bấm 1 dòng mở chi tiết ngay dưới dòng. */
export function SupportTicketsTable({ rows, counts, filter, onFilter, query, onQuery, openId, onToggle, note }: SupportTicketsTableProps) {
  const { t } = useI18n();
  const today = new Date();

  return (
    <>
      <div className={styles.toolbar}>
        <div className={styles.tabs} role="tablist" aria-label={t('Lọc yêu cầu')}>
          {TICKET_TABS.map(tab => (
            <button key={tab.key} type="button" role="tab" aria-selected={filter === tab.key} className={cx(filter === tab.key && styles.on)} onClick={() => onFilter(tab.key)}>
              {t(tab.label)} <em>{counts[tab.key]}</em>
            </button>
          ))}
        </div>
        <label className={styles.search}>
          <Icon name="search" size={13} />
          <input value={query} onChange={e => onQuery(e.target.value)} placeholder={t('Tìm mã yêu cầu, vận đơn, nội dung…')} aria-label={t('Tìm yêu cầu')} />
        </label>
      </div>

      {note ? (
        <p className={styles.note}>{note}</p>
      ) : (
        <div className={styles.scroll}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t('Mã')}</th>
                <th>{t('Loại')}</th>
                <th>{t('Vấn đề')}</th>
                <th>{t('Vận đơn')}</th>
                <th>{t('Nội dung')}</th>
                <th>{t('Gửi lúc')}</th>
                <th>{t('Cập nhật')}</th>
                <th>{t('Trạng thái')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(x => {
                const open = openId === x.id;
                return (
                  <Fragment key={x.id}>
                    <tr id={ticketRowId(x.id)} className={cx(styles.item, open && styles.open)} onClick={() => onToggle(x)}
                      aria-expanded={open}>
                      <td className="mono">
                        <button type="button" className={styles.code} onClick={e => { e.stopPropagation(); onToggle(x); }}>{x.id}</button>
                      </td>
                      <td>{t(SUPPORT_KIND_SHORT[kindOf(x)])}</td>
                      <td>{t(x.type)}</td>
                      <td className="mono">{x.bill || <span className={styles.muted}>—</span>}</td>
                      <td><span className={styles.desc}>{x.desc}</span></td>
                      <td className={styles.nowrap}>{compactTime(x.date, today)}</td>
                      <td className={styles.nowrap}>{compactTime(x.updatedAt ?? x.date, today)}</td>
                      <td className={styles.status}>
                        <StatusPill tone={SUPPORT_STATUS[x.status].tone}>{SUPPORT_STATUS[x.status].label}</StatusPill>
                        {kindOf(x) !== 'gop_y' && x.lv !== 'low' && x.status !== 'done' && (
                          <StatusPill tone={SUPPORT_PRIORITY[x.lv].tone}>{SUPPORT_PRIORITY[x.lv].label}</StatusPill>
                        )}
                      </td>
                    </tr>
                    {open && (
                      <tr className={styles.detailRow}>
                        <td colSpan={8}>
                          <SupportTicketDetail ticket={x} />
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
