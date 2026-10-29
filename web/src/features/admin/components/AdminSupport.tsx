import { useMemo, useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, DataTable, Drawer, Notice, StatusPill, TextAreaField, useToast, type Column } from '@/shared/ui';
import { FilterBar, type FilterOption } from '@/features/notifications';
import { kindOf, SupportThreadList, ticketTitle, SUPPORT_PRIORITY, SUPPORT_STATUS, type SupportStatus } from '@/features/support';
import { useAdminSupportTickets, useReplyAdminSupportTicket, type AdminSupportTicket } from '../api';
import { adminKindLabel, filterSupportTickets, isContactMessage, matchesFilter, matchesStaff, staffOptions, SUPPORT_FILTERS, ticketActions, UNASSIGNED, type SupportFilter } from '../lib/support';
import styles from './AdminSupport.module.css';

/**
 * Tab "Hỗ trợ & sự cố" của trang quản trị: lọc theo trạng thái + nhân viên phụ trách + tìm, bảng mỗi yêu cầu 1 dòng,
 * bấm 1 dòng → khung chi tiết; mỗi nút ở chân khung đổi trạng thái kèm câu trả lời (nếu có) — lưu vào yêu cầu, khách xem ở trang Hỗ trợ & góp ý.
 */
export function AdminSupport() {
  const { t } = useI18n();
  const list = useAdminSupportTickets(true);
  const [filter, setFilter] = useState<SupportFilter>('open');
  const [staff, setStaff] = useState('');
  const [query, setQuery] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);

  const rows = useMemo(() => list.data ?? [], [list.data]);
  const staffList = useMemo(() => staffOptions(rows), [rows]);
  const ofStaff = useMemo(() => rows.filter(x => matchesStaff(x, staff)), [rows, staff]);
  const shown = useMemo(() => filterSupportTickets(ofStaff, filter, query), [ofStaff, filter, query]);
  const current = rows.find(x => x.ticket.id === openId) ?? null;
  const options: ReadonlyArray<FilterOption<SupportFilter>> = SUPPORT_FILTERS.map(f => ({
    ...f,
    count: ofStaff.filter(x => matchesFilter(x, f.key)).length,
    hot: f.key === 'new'
  }));

  const columns: ReadonlyArray<Column<AdminSupportTicket>> = [
    { key: 'id', header: 'Mã', width: 90, render: x => <span className={cx('mono', x.ticket.status === 'new' && styles.strong)}>{x.ticket.id}</span> },
    { key: 'date', header: 'Gửi lúc', width: 130, render: x => <span className={styles.nowrap}>{x.ticket.date}</span> },
    {
      key: 'customer',
      header: 'Khách hàng',
      render: x => <span className={styles.cell}>{x.companyName || x.customerCode}<small>{x.customerCode}</small></span>
    },
    { key: 'bill', header: 'Vận đơn', width: 110, render: x => (x.ticket.bill ? <span className="mono">{x.ticket.bill}</span> : '—') },
    { key: 'kind', header: 'Loại', width: 110, render: x => <span className={styles.nowrap}>{t(adminKindLabel(x.ticket))}</span> },
    { key: 'title', header: 'Vấn đề', render: x => t(ticketTitle(x.ticket)) },
    { key: 'desc', header: 'Nội dung', width: '24%', render: x => <span className={styles.clip} title={x.ticket.desc}>{x.ticket.desc}</span> },
    {
      key: 'lv',
      header: 'Mức độ',
      width: 90,
      render: x =>
        kindOf(x.ticket) === 'su_co' ? <span className={cx(x.ticket.lv === 'high' && x.ticket.status !== 'done' && styles.hot)}>{t(SUPPORT_PRIORITY[x.ticket.lv].label)}</span> : '—'
    },
    { key: 'staff', header: 'Nhân viên', render: x => <span className={styles.nowrap}>{x.managerName || <span className={styles.faint}>{t('Chưa phân công')}</span>}</span> },
    { key: 'status', header: 'Trạng thái', width: 110, render: x => <StatusPill tone={SUPPORT_STATUS[x.ticket.status].tone}>{t(SUPPORT_STATUS[x.ticket.status].label)}</StatusPill> }
  ];

  return (
    <div className={styles.wrap}>
      <div>
        <h2 className={styles.heading}>{t('Hỗ trợ & sự cố')}</h2>
        <p className={styles.sub}>{t('Sự cố vận đơn, yêu cầu hỗ trợ và góp ý của khách')}</p>
      </div>

      <FilterBar options={options} value={filter} onChange={setFilter} query={query} onQuery={setQuery} placeholder="Tìm mã, vận đơn, khách">
        <select className={styles.select} value={staff} onChange={e => setStaff(e.target.value)} aria-label={t('Nhân viên phụ trách')}>
          <option value="">{t('Nhân viên: tất cả')}</option>
          {staffList.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          <option value={UNASSIGNED}>{t('Chưa phân công')}</option>
        </select>
      </FilterBar>

      <div className={styles.card}>
        {list.isError ? (
          <div className={styles.note}><Notice tone="danger">{getErrorMessage(list.error)}</Notice></div>
        ) : (
          <DataTable
            columns={columns}
            rows={shown}
            rowKey={x => x.ticket.id}
            loading={list.isLoading}
            empty={{ title: 'Không có yêu cầu nào.' }}
            onRowClick={x => setOpenId(x.ticket.id)}
            highlight={x => x.ticket.id === openId}
            minWidth={1180}
            caption="Hỗ trợ & sự cố"
          />
        )}
      </div>
      {!!shown.length && <p className={styles.count}>{t('{n} yêu cầu · bấm vào dòng để xem và trả lời', { n: shown.length })}</p>}

      <TicketDrawer item={current} onClose={() => setOpenId(null)} />
    </div>
  );
}

function TicketDrawer({ item, onClose }: { item: AdminSupportTicket | null; onClose: () => void }) {
  const { t } = useI18n();
  const toast = useToast();
  const reply = useReplyAdminSupportTicket();
  const [note, setNote] = useState('');

  const close = () => {
    setNote('');
    reply.reset();
    onClose();
  };
  const send = (status?: SupportStatus) =>
    item &&
    reply.mutate(
      { id: item.ticket.id, reply: note, status },
      { onSuccess: res => { toast.show(t(res.message), 'success'); close(); } }
    );

  const x = item?.ticket;
  // Lời nhắn từ trang chủ: khách chưa có tài khoản để xem trả lời → nhân viên gọi / email rồi đổi trạng thái.
  const contact = !!x && isContactMessage(x);
  return (
    <Drawer
      open={!!item}
      title={x ? `${t(adminKindLabel(x))} · ${t(SUPPORT_STATUS[x.status].label)}` : 'Hỗ trợ & sự cố'}
      onClose={close}
      footer={
        x && (
          <>
            {note.trim() && (
              <Button variant="ghost" className={styles.leftBtn} disabled={reply.isPending} onClick={() => send()}>
                {t('Chỉ gửi trả lời')}
              </Button>
            )}
            {ticketActions(x.status, !contact).map(a => (
              <Button key={a.status} variant={a.primary ? 'primary' : undefined} disabled={reply.isPending} onClick={() => send(a.status)}>
                {t(a.label)}
              </Button>
            ))}
          </>
        )
      }
    >
      {item && x && (
        <>
          <h3 className={styles.dTitle}><span className="mono">{x.id}</span> · {t(ticketTitle(x))}</h3>
          <dl className={styles.facts}>
            <dt>{t('Khách')}</dt>
            <dd>{contact ? t('Khách vãng lai (chưa đăng nhập)') : `${item.companyName || '—'} · ${item.customerCode}`}</dd>
            <dt>{t('Người gửi')}</dt>
            <dd>
              {x.req}{x.contact && ` · ${x.contact}`}
              {contact ? item.email && <small>{item.email}</small> : <small>{t(item.isStaff ? 'Tài khoản con' : 'Tài khoản chính')}</small>}
            </dd>
            {x.bill && (
              <>
                <dt>{t('Vận đơn')}</dt>
                <dd><span className="mono">{x.bill}</span>{(x.cnee || x.ct) && <small>{[x.cnee, x.ct].filter(Boolean).join(' · ')}</small>}</dd>
                <dt>{t('Mức độ')}</dt>
                <dd>{t(SUPPORT_PRIORITY[x.lv].label)}</dd>
              </>
            )}
            {kindOf(x) === 'ho_tro' && (
              <>
                <dt>{t('Chủ đề')}</dt>
                <dd>{t(x.type)}</dd>
              </>
            )}
            <dt>{t('Nhân viên')}</dt>
            <dd>{item.managerName || t('Chưa phân công')}</dd>
            <dt>{t('Gửi lúc')}</dt>
            <dd>{x.date}{x.updatedAt && <small>{t('Cập nhật {at}', { at: x.updatedAt })}</small>}</dd>
          </dl>
          <p className={styles.desc}>{x.desc}</p>
          {!!x.thread?.length && (
            <>
              <h4 className={styles.dSub}>{t('Trao đổi')}</h4>
              <SupportThreadList ticket={x} viewer="cs" />
            </>
          )}
          {reply.isError && <Notice tone="danger">{getErrorMessage(reply.error)}</Notice>}
          {contact ? (
            <Notice tone="info">{t('Khách chưa có tài khoản portal — gọi điện hoặc email để trả lời, xong thì đổi trạng thái.')}</Notice>
          ) : (
            <TextAreaField
              label="Trả lời khách (không bắt buộc)"
              rows={4}
              maxLength={4000}
              value={note}
              onChange={e => setNote(e.target.value)}
              hint={t('Lưu vào yêu cầu, khách xem ở trang Hỗ trợ & góp ý. Bấm "Chờ khách" để khách trả lời lại.')}
            />
          )}
        </>
      )}
    </Drawer>
  );
}
