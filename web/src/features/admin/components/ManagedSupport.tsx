import { useMemo, useState } from 'react';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, useToast } from '@/shared/ui';
import {
  FilterSelect, kindOf, lastActivity, periodStart, SupportFilterBar, SupportFooterNote, SupportHeader, SupportOverview, SupportReplyBox,
  SupportShell, SupportSplit, SupportTicketDetail, SupportTicketList, SUPPORT_PRIORITY, TICKET_PAGE_SIZE, TICKET_PERIODS, withinPeriod,
  type DetailInfo, type SupportStatus, type TicketPeriod
} from '@/features/support';
import { useAdminSupportTickets, useManagedSupportLive, useReplyAdminSupportTicket, type AdminSupportTicket } from '../api';
import {
  CS_REPLY_TEMPLATES, defaultManagedFilter, filterSupportTickets, isContactMessage, managedCounts, MANAGED_KINDS, MANAGED_TILES, matchesCustomer,
  matchesKind, matchesStaff, staffOptions, ticketActions, UNASSIGNED, type ManagedFilter, type ManagedKind
} from '../lib/support';
import styles from './ManagedSupport.module.css';

/**
 * Hỗ trợ & góp ý khi nhân viên Việt An đăng nhập portal — cùng bố cục trang của khách (features/support):
 * ô trạng thái · hàng lọc (khách, nhân viên, loại, ngày) · danh sách thẻ | chi tiết + trả lời (câu mẫu) + đổi trạng thái.
 * Chỉ yêu cầu của khách trong phạm vi (backend lọc); đã chọn khách thì mặc định chỉ yêu cầu của khách đó.
 */
export function ManagedSupport({ actingCustomer }: { actingCustomer?: { code: string; name: string } }) {
  const { t } = useI18n();
  const list = useAdminSupportTickets(true, 'managed');
  // Khách gửi / trả lời → hiện ngay (SignalR), không cần tải lại trang.
  useManagedSupportLive();
  const [filter, setFilter] = useState<ManagedFilter | null>(null);
  const [kind, setKind] = useState<ManagedKind>('');
  const [period, setPeriod] = useState<TicketPeriod>('');
  const [query, setQuery] = useState('');
  const [staff, setStaff] = useState('');
  const [onlyActing, setOnlyActing] = useState(true);
  const [newestFirst, setNewestFirst] = useState(true);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);

  const items = useMemo(() => list.data ?? [], [list.data]);
  const customerCode = actingCustomer && onlyActing ? actingCustomer.code : '';
  // Phạm vi đang xem (khách + nhân viên) — ô trạng thái đếm trong phạm vi này.
  const scoped = useMemo(() => items.filter(x => matchesCustomer(x, customerCode) && matchesStaff(x, staff)), [items, customerCode, staff]);
  const counts = useMemo(() => managedCounts(scoped), [scoped]);
  const active = filter ?? (list.isLoading ? 'new' : defaultManagedFilter(scoped));
  const rows = useMemo(() => {
    const from = periodStart(period, new Date());
    const shown = filterSupportTickets(scoped.filter(x => matchesKind(x, kind) && withinPeriod(x.ticket, from)), active, query)
      .sort((a, b) => lastActivity(b.ticket) - lastActivity(a.ticket));
    return newestFirst ? shown : shown.reverse();
  }, [scoped, kind, period, active, query, newestFirst]);
  const pageCount = Math.max(1, Math.ceil(rows.length / TICKET_PAGE_SIZE));
  const shownPage = Math.min(page, pageCount);
  const pageRows = rows.slice((shownPage - 1) * TICKET_PAGE_SIZE, shownPage * TICKET_PAGE_SIZE);
  const byId = useMemo(() => new Map(items.map(x => [x.ticket.id, x])), [items]);
  const current = openId ? byId.get(openId) ?? null : null;
  const tile = MANAGED_TILES.find(x => x.key === active)!;

  const reset = <T,>(set: (v: T) => void) => (v: T) => { set(v); setPage(1); };
  const filtered = !!(kind || period || query || staff || (actingCustomer && !onlyActing));
  const clearFilters = () => {
    setKind('');
    setPeriod('');
    setQuery('');
    setStaff('');
    setOnlyActing(true);
    setPage(1);
  };

  const customerOptions = actingCustomer
    ? [{ value: 'acting', label: actingCustomer.name || actingCustomer.code }, { value: '', label: 'Tất cả khách' }]
    : [];
  const staffList = useMemo(() => staffOptions(items), [items]);

  const note = list.isLoading ? t('Đang tải…')
    : list.isError ? (isNotImplemented(list.error) ? t('Chức năng hỗ trợ đang được kết nối máy chủ.') : getErrorMessage(list.error))
      : !rows.length ? t('Không có yêu cầu nào ở mục này.')
        : null;

  return (
    <SupportShell>
      <SupportHeader title="Hỗ trợ & góp ý" sub={actingCustomer && onlyActing ? 'Yêu cầu của khách đang chọn' : 'Yêu cầu của khách bạn phụ trách'} />

      <SupportOverview tiles={MANAGED_TILES} counts={counts} value={active} onChange={reset(setFilter)} />

      <SupportFilterBar query={query} onQuery={reset(setQuery)} placeholder="Tìm mã, vận đơn, khách, nội dung…" canReset={filtered} onReset={clearFilters}>
        {actingCustomer && (
          <FilterSelect label="Khách" value={onlyActing ? 'acting' : ''} options={customerOptions} onChange={v => reset(setOnlyActing)(v === 'acting')} />
        )}
        <FilterSelect
          label="Nhân viên"
          value={staff}
          options={[{ value: '', label: 'Tất cả' }, ...staffList.map(s => ({ value: s.id, label: s.name })), { value: UNASSIGNED, label: 'Chưa phân công' }]}
          onChange={reset(setStaff)}
        />
        <FilterSelect label="Loại" value={kind} options={MANAGED_KINDS} onChange={reset(setKind)} />
        <FilterSelect label="Ngày" value={period} options={TICKET_PERIODS} onChange={reset(setPeriod)} />
      </SupportFilterBar>

      <SupportSplit
        list={
          <SupportTicketList
            viewer="cs"
            heading="Yêu cầu của khách"
            rows={pageRows.map(x => x.ticket)}
            subline={x => customerLabel(byId.get(x.id), t)}
            total={rows.length}
            group={tile}
            newestFirst={newestFirst}
            onToggleSort={() => setNewestFirst(v => !v)}
            openId={openId}
            onOpen={x => setOpenId(x.id)}
            page={shownPage}
            pageCount={pageCount}
            pageSize={TICKET_PAGE_SIZE}
            onPage={setPage}
            note={note}
          />
        }
        pane={current ? <ManagedDetail key={current.ticket.id} item={current} /> : <p className={styles.empty}>{t('Chọn 1 yêu cầu bên trái để xem và trả lời khách.')}</p>}
      />
    </SupportShell>
  );
}

const customerLabel = (x: AdminSupportTicket | undefined, t: (s: string) => string) =>
  !x ? '' : isContactMessage(x.ticket) ? t('Khách vãng lai (chưa đăng nhập)') : `${x.companyName || '—'} · ${x.customerCode}`;

/** Chi tiết 1 yêu cầu ở trang CS: thông tin khách + lịch sử; đáy khung = trả lời (câu mẫu) + nút đổi trạng thái. */
function ManagedDetail({ item }: { item: AdminSupportTicket }) {
  const { t } = useI18n();
  const toast = useToast();
  const reply = useReplyAdminSupportTicket('managed');
  const [text, setText] = useState('');
  const x = item.ticket;
  const contact = isContactMessage(x);

  const info: DetailInfo[] = [
    { label: 'Khách', value: contact ? t('Khách vãng lai') : item.companyName || item.customerCode, sub: contact ? undefined : item.customerCode },
    { label: 'Người gửi', value: x.req || '—', sub: [x.contact, contact ? item.email : t(item.isStaff ? 'Tài khoản con' : 'Tài khoản chính')].filter(Boolean).join(' · ') },
    ...(x.bill ? [{ label: 'Vận đơn', value: x.bill, sub: [x.cnee, x.ct].filter(Boolean).join(' · ') }] : []),
    ...(!contact && kindOf(x) !== 'gop_y' ? [{ label: 'Mức độ', value: t(SUPPORT_PRIORITY[x.lv].label), hot: x.lv !== 'low' }] : []),
    { label: 'Nhân viên', value: item.managerName || t('Chưa phân công') }
  ];

  const send = (status?: SupportStatus) =>
    reply.mutate(
      { id: x.id, reply: text, status },
      {
        onSuccess: res => { toast.show(t(res.message), 'success'); setText(''); },
        onError: e => toast.show(getErrorMessage(e), 'error')
      }
    );
  const actions = ticketActions(x.status, !contact).map(a => (
    <Button key={a.status} size="sm" variant={a.primary ? 'primary' : undefined} disabled={reply.isPending} onClick={() => send(a.status)}>
      {t(a.label)}
    </Button>
  ));

  const footer = contact ? (
    <SupportFooterNote>
      {t('Khách chưa có tài khoản portal — gọi điện hoặc email để trả lời, xong thì đổi trạng thái.')}
      <span className={styles.noteActions}>{actions}</span>
    </SupportFooterNote>
  ) : (
    <SupportReplyBox
      id={x.id}
      label="Trả lời khách"
      value={text}
      onChange={setText}
      placeholder="Trả lời khách… (lưu vào yêu cầu, khách xem ở trang Hỗ trợ & góp ý)"
      onSubmit={() => text.trim() && send()}
      tools={
        <select className={styles.template} value="" onChange={e => e.target.value && setText(e.target.value)} aria-label={t('Trả lời mẫu')}>
          <option value="">{t('Trả lời mẫu…')}</option>
          {CS_REPLY_TEMPLATES.map(r => <option key={r} value={r}>{t(r)}</option>)}
        </select>
      }
      actions={
        <>
          {text.trim() && (
            <Button size="sm" disabled={reply.isPending} onClick={() => send()}>
              <Icon name="send" size={14} /> {t('Gửi')}
            </Button>
          )}
          {actions}
        </>
      }
    />
  );

  return <SupportTicketDetail ticket={x} viewer="cs" info={info} footer={footer} />;
}
