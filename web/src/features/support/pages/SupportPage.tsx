import { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { useSupportTickets } from '../api';
import { SupportContacts } from '../components/SupportContacts';
import { SupportRequestForm } from '../components/SupportRequestForm';
import { SupportTicketsTable, ticketRowId } from '../components/SupportTicketsTable';
import { SENDER_INFO_TOPIC } from '../constants';
import type { RequestPreset } from '../lib/draft';
import { defaultFilter, filterCounts, filterTickets, type TicketFilter } from '../lib/requests';
import type { SupportTicket } from '../types';
import styles from './SupportPage.module.css';

/**
 * Hỗ trợ & góp ý (dbo.TContact, như form "Yêu cầu" của Bill Online cũ): trên là form gửi yêu cầu mới + liên hệ nhanh,
 * dưới là bảng yêu cầu đã gửi — bấm 1 dòng xem chi tiết và trao đổi với CS; CS "Chờ khách phản hồi" thì khách trả lời ngay trong dòng đó.
 * Link: ?bill=… điền sẵn mã vận đơn; ?new=sender-info chọn sẵn vấn đề "Sửa thông tin người gửi"; ?id=TRB-… mở sẵn yêu cầu đó.
 */
export default function SupportPage() {
  const { t } = useI18n();
  const { data: tickets = [], isLoading, isError, error } = useSupportTickets();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<TicketFilter | null>(null);
  const [query, setQuery] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);
  const [preset, setPreset] = useState<RequestPreset>({});
  const [formKey, setFormKey] = useState(0);
  const [scrollTo, setScrollTo] = useState<string | null>(null);
  const formRef = useRef<HTMLElement>(null);

  const ready = !isLoading;
  const active = filter ?? (ready ? defaultFilter(tickets) : 'open');
  const rows = useMemo(() => filterTickets(tickets, active, query), [tickets, active, query]);
  const counts = useMemo(() => filterCounts(tickets), [tickets]);

  // Link từ đơn hàng / form tạo đơn
  useEffect(() => {
    const bill = params.get('bill');
    const id = params.get('id');
    const isNew = params.get('new') === 'sender-info';
    if (!bill && !id && !isNew) return;
    if (bill || isNew) {
      setPreset(bill ? { kind: 'ho_tro', bill } : { kind: 'ho_tro', topic: SENDER_INFO_TOPIC });
      setFormKey(k => k + 1);
      formRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else if (id) {
      setFilter('all');
      setOpenId(id.toUpperCase());
      setScrollTo(id.toUpperCase());
    }
    setParams({}, { replace: true });
  }, [params, setParams]);

  // Mở từ link ?id= / vừa gửi xong: đợi bảng có dòng rồi cuộn tới
  useEffect(() => {
    if (!scrollTo || !ready) return;
    document.getElementById(ticketRowId(scrollTo))?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    setScrollTo(null);
  }, [scrollTo, ready, rows]);

  const toggle = (ticket: SupportTicket) => setOpenId(openId === ticket.id ? null : ticket.id);

  const onSent = (ticket: SupportTicket) => {
    setFilter('open');
    setQuery('');
    setOpenId(ticket.id);
    setPreset({});
    setScrollTo(ticket.id);
  };

  const note = isLoading ? t('Đang tải…')
    : isError ? t(isNotImplemented(error) ? 'Chức năng hỗ trợ đang được kết nối máy chủ. Cần gấp, vui lòng gọi hotline Việt An.' : 'Không tải được danh sách yêu cầu')
      : !rows.length ? t(tickets.length ? 'Không có yêu cầu nào ở mục này.' : 'Chưa có yêu cầu nào. Điền form phía trên khi cần CS hỗ trợ.')
        : null;

  return (
    <div className={styles.page}>
      <header className={styles.head}>
        <h1>{t('Hỗ trợ & góp ý')}</h1>
        <p>{t('Gửi yêu cầu cho CS Việt An và theo dõi kết quả. CS trả lời ngay trong từng yêu cầu bên dưới.')}</p>
      </header>

      <div className={styles.top}>
        <section className={styles.box} ref={formRef}>
          <h2>{t('Gửi yêu cầu mới')}</h2>
          <div className={styles.pad}>
            <SupportRequestForm key={formKey} preset={preset} onSent={onSent} />
          </div>
        </section>
        <section className={styles.box}>
          <h2>{t('Liên hệ nhanh')}</h2>
          <div className={styles.pad}>
            <SupportContacts />
          </div>
        </section>
      </div>

      <section className={styles.box}>
        <h2>
          {t('Yêu cầu đã gửi')} <small>{t('bấm 1 dòng để xem chi tiết và trao đổi với CS')}</small>
        </h2>
        <SupportTicketsTable
          rows={rows}
          counts={counts}
          filter={active}
          onFilter={setFilter}
          query={query}
          onQuery={setQuery}
          openId={openId}
          onToggle={toggle}
          note={note}
        />
      </section>
    </div>
  );
}
