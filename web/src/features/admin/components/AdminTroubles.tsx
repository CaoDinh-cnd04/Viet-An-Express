import { useMemo, useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, DataTable, Drawer, Notice, StatusPill, TextAreaField, useToast, type Column } from '@/shared/ui';
import { FilterBar, type FilterOption } from '@/features/notifications';
import { kindOf, ticketTitle, TROUBLE_PRIORITY, TROUBLE_STATUS, type TroubleStatus } from '@/features/troubles';
import { useAdminTroubles, useReplyAdminTrouble, type AdminTrouble } from '../api';
import { adminKindLabel, filterTroubles, isContactMessage, matchesFilter, matchesStaff, staffOptions, TROUBLE_FILTERS, troubleActions, UNASSIGNED, type TroubleFilter } from '../lib/troubles';
import styles from './AdminTroubles.module.css';

/**
 * Tab "Hỗ trợ & sự cố" của trang quản trị: lọc theo trạng thái + nhân viên phụ trách + tìm, bảng mỗi yêu cầu 1 dòng,
 * bấm 1 dòng → khung chi tiết; mỗi nút ở chân khung đổi trạng thái (kèm ghi chú nếu có), khách nhận thông báo riêng.
 */
export function AdminTroubles() {
  const { t } = useI18n();
  const list = useAdminTroubles(true);
  const [filter, setFilter] = useState<TroubleFilter>('open');
  const [staff, setStaff] = useState('');
  const [query, setQuery] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);

  const rows = useMemo(() => list.data ?? [], [list.data]);
  const staffList = useMemo(() => staffOptions(rows), [rows]);
  const ofStaff = useMemo(() => rows.filter(x => matchesStaff(x, staff)), [rows, staff]);
  const shown = useMemo(() => filterTroubles(ofStaff, filter, query), [ofStaff, filter, query]);
  const current = rows.find(x => x.ticket.id === openId) ?? null;
  const options: ReadonlyArray<FilterOption<TroubleFilter>> = TROUBLE_FILTERS.map(f => ({
    ...f,
    count: ofStaff.filter(x => matchesFilter(x, f.key)).length,
    hot: f.key === 'new'
  }));

  const columns: ReadonlyArray<Column<AdminTrouble>> = [
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
        kindOf(x.ticket) === 'su_co' ? <span className={cx(x.ticket.lv === 'high' && x.ticket.status !== 'done' && styles.hot)}>{t(TROUBLE_PRIORITY[x.ticket.lv].label)}</span> : '—'
    },
    { key: 'staff', header: 'Nhân viên', render: x => <span className={styles.nowrap}>{x.managerName || <span className={styles.faint}>{t('Chưa phân công')}</span>}</span> },
    { key: 'status', header: 'Trạng thái', width: 110, render: x => <StatusPill tone={TROUBLE_STATUS[x.ticket.status].tone}>{t(TROUBLE_STATUS[x.ticket.status].label)}</StatusPill> }
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

      <TroubleDrawer item={current} onClose={() => setOpenId(null)} />
    </div>
  );
}

function TroubleDrawer({ item, onClose }: { item: AdminTrouble | null; onClose: () => void }) {
  const { t } = useI18n();
  const toast = useToast();
  const reply = useReplyAdminTrouble();
  const [note, setNote] = useState('');

  const close = () => {
    setNote('');
    reply.reset();
    onClose();
  };
  const send = (status?: TroubleStatus) =>
    item &&
    reply.mutate(
      { id: item.ticket.id, reply: note, status },
      { onSuccess: res => { toast.show(t(res.message), 'success'); close(); } }
    );

  const x = item?.ticket;
  // Lời nhắn từ trang chủ: khách chưa có tài khoản → không gửi ghi chú được, nhân viên gọi / email rồi đổi trạng thái.
  const contact = !!x && isContactMessage(x);
  return (
    <Drawer
      open={!!item}
      title={x ? `${t(adminKindLabel(x))} · ${t(TROUBLE_STATUS[x.status].label)}` : 'Hỗ trợ & sự cố'}
      onClose={close}
      footer={
        x && (
          <>
            {note.trim() && (
              <Button variant="ghost" className={styles.leftBtn} disabled={reply.isPending} onClick={() => send()}>
                {t('Chỉ gửi ghi chú')}
              </Button>
            )}
            {troubleActions(x.status, !contact).map(a => (
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
                <dd>{t(TROUBLE_PRIORITY[x.lv].label)}</dd>
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
          {reply.isError && <Notice tone="danger">{getErrorMessage(reply.error)}</Notice>}
          {contact ? (
            <Notice tone="info">{t('Khách chưa có tài khoản portal — gọi điện hoặc email để trả lời, xong thì đổi trạng thái.')}</Notice>
          ) : (
            <TextAreaField
              label="Ghi chú gửi khách (không bắt buộc)"
              rows={4}
              maxLength={4000}
              value={note}
              onChange={e => setNote(e.target.value)}
              hint={t('Bấm nút trạng thái bên dưới — khách nhận thông báo riêng kèm ghi chú')}
            />
          )}
        </>
      )}
    </Drawer>
  );
}
