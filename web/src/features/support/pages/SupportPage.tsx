import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Icon } from '@/shared/ui';
import { useSupportLive, useSupportTickets } from '../api';
import { NewSupportRequestDialog } from '../components/NewSupportRequestDialog';
import { SupportFaq } from '../components/SupportFaq';
import { FilterSelect, SupportFilterBar, SupportHeader, SupportHotline, SupportShell, SupportSplit } from '../components/SupportLayout';
import { SupportOverview } from '../components/SupportOverview';
import { SupportTicketDetail } from '../components/SupportTicketDetail';
import { SupportTicketList, ticketRowId } from '../components/SupportTicketList';
import { CONTACTS, SENDER_INFO_TOPIC, TICKET_KIND_FILTERS, TICKET_PAGE_SIZE, TICKET_PERIODS, TICKET_TILES } from '../constants';
import type { RequestPreset } from '../lib/draft';
import { defaultFilter, filterCounts, filterTickets, type TicketFilter, type TicketKindFilter, type TicketPeriod } from '../lib/requests';
import type { SupportTicket } from '../types';

/**
 * Hỗ trợ & góp ý của khách (dbo.TContact, như form "Yêu cầu" của Bill Online cũ):
 * - Đầu trang: hotline, nút Câu hỏi thường gặp + Tạo yêu cầu (hộp thoại); 4 ô trạng thái như trang Đơn hàng; hàng lọc Tìm · Ngày · Loại · Đặt lại.
 * - Thân: trái danh sách thẻ yêu cầu (phân trang), phải chi tiết + lịch sử trao đổi + ô trả lời khi CS chờ khách;
 *   chưa chọn yêu cầu thì bên phải là Câu hỏi thường gặp (bớt yêu cầu không cần thiết).
 * Link: ?bill=… mở hộp gửi yêu cầu điền sẵn vận đơn; ?new=sender-info chọn sẵn "Sửa thông tin người gửi"; ?id=TRB-… mở sẵn yêu cầu đó.
 */
export default function SupportPage() {
  const { t } = useI18n();
  const { data: tickets = [], isLoading, isError, error } = useSupportTickets();
  // CS trả lời / đổi trạng thái → hiện ngay (SignalR), không cần tải lại trang.
  useSupportLive();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<TicketFilter | null>(null);
  const [kind, setKind] = useState<TicketKindFilter>('');
  const [period, setPeriod] = useState<TicketPeriod>('');
  const [query, setQuery] = useState('');
  const [newestFirst, setNewestFirst] = useState(true);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);
  const [faq, setFaq] = useState(false);
  const [compose, setCompose] = useState<RequestPreset | null>(null);
  const [scrollTo, setScrollTo] = useState<string | null>(null);

  const ready = !isLoading;
  const active = filter ?? (ready ? defaultFilter(tickets) : 'waitc');
  const counts = useMemo(() => filterCounts(tickets), [tickets]);
  const rows = useMemo(() => {
    const list = filterTickets(tickets, { filter: active, kind, period, query });
    return newestFirst ? list : list.reverse();
  }, [tickets, active, kind, period, query, newestFirst]);
  const pageCount = Math.max(1, Math.ceil(rows.length / TICKET_PAGE_SIZE));
  const shownPage = Math.min(page, pageCount);
  const pageRows = rows.slice((shownPage - 1) * TICKET_PAGE_SIZE, shownPage * TICKET_PAGE_SIZE);
  const current = openId ? tickets.find(x => x.id === openId) ?? null : null;
  const tile = TICKET_TILES.find(x => x.key === active)!;

  // Đổi lọc → về trang 1
  const reset = <T,>(set: (v: T) => void) => (v: T) => { set(v); setPage(1); };

  // Link từ đơn hàng / form tạo đơn
  useEffect(() => {
    const bill = params.get('bill');
    const id = params.get('id');
    const isNew = params.get('new') === 'sender-info';
    if (!bill && !id && !isNew) return;
    if (bill) setCompose({ kind: 'ho_tro', bill });
    else if (isNew) setCompose({ kind: 'ho_tro', topic: SENDER_INFO_TOPIC });
    else if (id) {
      setFilter('all');
      setOpenId(id.toUpperCase());
      setScrollTo(id.toUpperCase());
    }
    setParams({}, { replace: true });
  }, [params, setParams]);

  // Mở từ link ?id= / vừa gửi xong: đợi danh sách có thẻ rồi cuộn tới
  useEffect(() => {
    if (!scrollTo || !ready) return;
    const i = rows.findIndex(x => x.id === scrollTo);
    if (i >= 0 && Math.floor(i / TICKET_PAGE_SIZE) + 1 !== shownPage) return setPage(Math.floor(i / TICKET_PAGE_SIZE) + 1);
    document.getElementById(ticketRowId(scrollTo))?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    setScrollTo(null);
  }, [scrollTo, ready, rows, shownPage]);

  const open = (ticket: SupportTicket) => {
    setOpenId(ticket.id);
    setFaq(false);
  };

  const onSent = (ticket: SupportTicket) => {
    setCompose(null);
    setFilter('doing');
    setKind('');
    setPeriod('');
    setQuery('');
    setPage(1);
    open(ticket);
    setScrollTo(ticket.id);
  };

  const clearFilters = () => {
    setKind('');
    setPeriod('');
    setQuery('');
    setPage(1);
  };
  const filtered = !!(kind || period || query);

  const note = isLoading ? t('Đang tải…')
    : isError ? t(isNotImplemented(error) ? 'Chức năng hỗ trợ đang được kết nối máy chủ. Cần gấp, vui lòng gọi hotline Việt An.' : 'Không tải được danh sách yêu cầu')
      : !rows.length ? t(tickets.length ? 'Không có yêu cầu nào ở mục này.' : 'Chưa có yêu cầu nào. Bấm "Tạo yêu cầu" khi cần CS hỗ trợ.')
        : null;

  return (
    <SupportShell>
      <SupportHeader title="Hỗ trợ & góp ý" sub="Theo dõi yêu cầu và trao đổi với đội ngũ chăm sóc khách hàng.">
        <SupportHotline href={CONTACTS[0]!.href} value={CONTACTS[0]!.value} />
        <Button size="sm" onClick={() => { setFaq(true); setOpenId(null); }} data-tour="support-faq">
          <Icon name="help" size={14} /> {t('Câu hỏi thường gặp')}
        </Button>
        <Button variant="primary" size="sm" onClick={() => setCompose({})} data-tour="support-new">
          <Icon name="plus" size={14} /> {t('Tạo yêu cầu')}
        </Button>
      </SupportHeader>

      <SupportOverview tiles={TICKET_TILES} counts={counts} value={active} onChange={reset(setFilter)} />

      <SupportFilterBar query={query} onQuery={reset(setQuery)} placeholder="Tìm mã yêu cầu, vận đơn hoặc nội dung…" canReset={filtered} onReset={clearFilters}>
        <FilterSelect label="Ngày" value={period} options={TICKET_PERIODS} onChange={reset(setPeriod)} />
        <FilterSelect label="Loại" value={kind} options={TICKET_KIND_FILTERS} onChange={reset(setKind)} />
      </SupportFilterBar>

      <SupportSplit
        tour="support-tickets"
        list={
          <SupportTicketList
            rows={pageRows}
            total={rows.length}
            group={tile}
            newestFirst={newestFirst}
            onToggleSort={() => setNewestFirst(v => !v)}
            openId={openId}
            onOpen={open}
            page={shownPage}
            pageCount={pageCount}
            pageSize={TICKET_PAGE_SIZE}
            onPage={setPage}
            note={note}
          />
        }
        pane={
          current && !faq ? (
            <SupportTicketDetail key={current.id} ticket={current} onNew={() => setCompose({ kind: 'ho_tro', bill: current.bill || undefined })} />
          ) : (
            <SupportFaq onClose={faq && openId ? () => setFaq(false) : undefined} onNew={() => setCompose({})} />
          )
        }
      />

      {compose && <NewSupportRequestDialog open preset={compose} onClose={() => setCompose(null)} onSent={onSent} />}
    </SupportShell>
    );
}
