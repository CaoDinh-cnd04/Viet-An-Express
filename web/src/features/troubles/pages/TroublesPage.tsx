import { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMarkRead, useNotifications } from '@/features/notifications';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { downloadTextFile } from '@/shared/lib/files';
import { Icon } from '@/shared/ui';
import { useTroubles } from '../api';
import { KIND_BADGE, NewConversationDialog } from '../components/NewConversationDialog';
import { CONTACTS, SENDER_INFO_TOPIC, TROUBLE_KINDS } from '../constants';
import { kindOf, ticketTitle } from '../lib/chat';
import {
  CHAT_STATUS, defaultFilter, filterConversations, filterCounts, isOpen, messagesOf, noticesByTicket, previewOf, transcriptOf, unreadOf,
  type ConversationFilter
} from '../lib/conversation';
import type { TroubleKind, TroubleTicket } from '../types';
import styles from './TroublesPage.module.css';

const FILTERS: ReadonlyArray<{ key: ConversationFilter; label: string }> = [
  { key: 'unread', label: 'Chưa đọc' },
  { key: 'open', label: 'Đang mở' },
  { key: 'closed', label: 'Đã đóng' },
  { key: 'all', label: 'Tất cả' }
];

type Preset = { kind?: TroubleKind; bill?: string; topic?: string };

/** Date → 'dd/MM/yyyy' (cùng dạng ngày của API). */
const dayKey = (d: Date) => `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;

/** "dd/MM/yyyy" → "Hôm nay" / "Hôm qua" / ngày. */
function dayLabel(day: string, today: Date): string {
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
  return day === dayKey(today) ? 'Hôm nay' : day === dayKey(yesterday) ? 'Hôm qua' : day;
}

/**
 * Hỗ trợ & góp ý dạng trò chuyện (như Zalo): trái danh sách cuộc trò chuyện (Chưa đọc / Đang mở / Đã đóng / Tất cả, tìm kiếm),
 * phải khung chat của cuộc đang chọn. Mục mở đầu: Chưa đọc nếu CS vừa trả lời, không thì Đang mở.
 * Yêu cầu TRB-… (dbo.TContact): tin CS = thông báo riêng CS gửi khách, khách không nhắn tiếp được.
 * Sự cố SC-… (dbo.Order_Trouble, đơn chưa giao): khách và CS nhắn qua lại ở ô cuối khung chat.
 * /troubles?bill=… mở hộp thoại báo sự cố cho đơn đó; ?new=sender-info mở hỗ trợ "Sửa thông tin người gửi"; ?id=TRB-… / SC-… mở cuộc đó.
 */
export default function TroublesPage() {
  const { t } = useI18n();
  const { data: tickets = [], isLoading, isError, error } = useTroubles();
  const notices = useNotifications();
  const markRead = useMarkRead();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<ConversationFilter | null>(null);
  const [query, setQuery] = useState('');
  const [currentId, setCurrentId] = useState<string | null>(null);
  const [keep, setKeep] = useState<string | null>(null);
  const [preset, setPreset] = useState<Preset | null>(null);
  const [chatting, setChatting] = useState(false);
  const msgsRef = useRef<HTMLDivElement>(null);
  const today = new Date();

  const byTicket = useMemo(() => noticesByTicket(notices.data?.data ?? []), [notices.data]);
  const ready = !isLoading && !notices.isLoading;
  const active = filter ?? (ready ? defaultFilter(tickets, byTicket) : 'open');
  const rows = useMemo(() => filterConversations(tickets, byTicket, active, query, keep), [tickets, byTicket, active, query, keep]);
  const counts = useMemo(() => filterCounts(tickets, byTicket), [tickets, byTicket]);
  const current = tickets.find(x => x.id === currentId) ?? null;
  const currentNotices = current ? byTicket.get(current.id) ?? [] : [];
  const messages = useMemo(() => (current ? messagesOf(current, currentNotices) : []), [current, currentNotices]);

  const open = (ticket: TroubleTicket) => {
    if (unreadOf(ticket.id, byTicket) > 0) setKeep(ticket.id);
    for (const n of byTicket.get(ticket.id) ?? []) if (n.unread) markRead.mutate(n.id);
    setCurrentId(ticket.id);
    setChatting(true);
  };

  // Lần đầu có dữ liệu: chốt mục mở đầu và mở sẵn cuộc đầu tiên (màn rộng).
  useEffect(() => {
    if (!ready || filter) return;
    const start = defaultFilter(tickets, byTicket);
    setFilter(start);
    const first = filterConversations(tickets, byTicket, start, '')[0];
    if (first && !currentId && window.matchMedia('(min-width: 861px)').matches) open(first);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready]);

  // Link từ đơn hàng / form tạo đơn / thông báo
  useEffect(() => {
    const bill = params.get('bill');
    const id = params.get('id');
    const isNew = params.get('new') === 'sender-info';
    if (!bill && !id && !isNew) return;
    if (bill) setPreset({ kind: 'su_co', bill });
    else if (isNew) setPreset({ kind: 'ho_tro', topic: SENDER_INFO_TOPIC });
    else if (id) {
      setFilter('all');
      setCurrentId(id.toUpperCase());
      setChatting(true);
    }
    setParams({}, { replace: true });
  }, [params, setParams]);

  // Cuộn xuống tin mới nhất khi đổi cuộc / có tin mới.
  useEffect(() => {
    const el = msgsRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [currentId, messages.length]);

  const changeFilter = (f: ConversationFilter) => {
    setFilter(f);
    setKeep(null);
  };

  return (
    <div className={styles.page}>
      <header className={styles.head}>
        <div>
          <h1>{t('Hỗ trợ & góp ý')}</h1>
          <p>{t('Nhắn cho CS Việt An — mỗi vấn đề là 1 cuộc trò chuyện.')}</p>
        </div>
        <span className={styles.hotline}>
          {t('Cần gấp? Gọi')} <a href={CONTACTS[0]!.href}>{CONTACTS[0]!.value}</a>
        </span>
      </header>

      <div className={cx(styles.shell, chatting && styles.chatting)}>
        <section className={styles.list}>
          <div className={styles.listHead}>
            <button type="button" className={styles.newBtn} onClick={() => setPreset({})}>
              <Icon name="plus" size={15} /> {t('Cuộc trò chuyện mới')}
            </button>
            <label className={styles.search}>
              <Icon name="search" size={14} />
              <input value={query} onChange={e => setQuery(e.target.value)} placeholder={t('Tìm mã, vận đơn, nội dung…')} aria-label={t('Tìm cuộc trò chuyện')} />
            </label>
            <div className={styles.seg} role="tablist" aria-label={t('Lọc cuộc trò chuyện')}>
              {FILTERS.map(f => (
                <button key={f.key} type="button" role="tab" aria-selected={active === f.key} className={cx(active === f.key && styles.on)} onClick={() => changeFilter(f.key)}>
                  {t(f.label)} <span className={cx(styles.n, f.key === 'unread' && counts.unread > 0 && styles.hot)}>{counts[f.key]}</span>
                </button>
              ))}
            </div>
          </div>

          <div className={styles.items}>
            {isLoading && <p className={styles.note}>{t('Đang tải…')}</p>}
            {isError && (
              <p className={styles.note}>
                {t(isNotImplemented(error) ? 'Chức năng hỗ trợ đang được kết nối máy chủ. Cần gấp, vui lòng gọi hotline Việt An.' : 'Không tải được danh sách yêu cầu')}
              </p>
            )}
            {!isLoading && !isError && !rows.length && (
              <p className={styles.note}>
                {t(tickets.length ? 'Không có cuộc trò chuyện nào ở mục này.' : 'Chưa có cuộc trò chuyện nào. Bấm "Cuộc trò chuyện mới" khi cần CS hỗ trợ.')}
              </p>
            )}
            {rows.map(x => {
              const kind = kindOf(x);
              const ns = byTicket.get(x.id) ?? [];
              const unread = unreadOf(x.id, byTicket);
              const preview = previewOf(x, ns);
              const last = ns.at(-1)?.date ?? x.updatedAt ?? x.date;
              const st = CHAT_STATUS[x.status];
              return (
                <button key={x.id} type="button" className={cx(styles.item, currentId === x.id && styles.itemOn, unread > 0 && styles.unread)} onClick={() => open(x)}>
                  <span className={cx(styles.badge, styles[kind])}>{KIND_BADGE[kind]}</span>
                  <span className={styles.itemText}>
                    <span className={styles.itemTitle}>
                      {t(ticketTitle(x))}{kind === 'su_co' && <> · <span className="mono">{x.bill}</span></>}
                    </span>
                    <span className={styles.preview}>{t(preview.from === 'cs' ? 'CS: {text}' : 'Bạn: {text}', { text: preview.text })}</span>
                  </span>
                  <span className={styles.itemSide}>
                    {last.slice(0, 10) === dayKey(today) ? last.slice(11, 16) : last.slice(0, 5)}
                    {unread > 0 ? <span className={styles.count}>{unread}</span> : <span className={cx(styles.chip, styles[st.tone])}>{t(st.label)}</span>}
                  </span>
                </button>
              );
            })}
          </div>
        </section>

        <section className={styles.chat}>
          {!current ? (
            <div className={styles.welcome}>
              <b>{t('Chọn 1 cuộc trò chuyện')}</b>
              {t('hoặc bấm "Cuộc trò chuyện mới" để nhắn CS Việt An.')}
            </div>
          ) : (
            <>
              <div className={styles.chatHead}>
                <button type="button" className={styles.backBtn} onClick={() => setChatting(false)} aria-label={t('Quay lại danh sách')}>‹</button>
                <span className={styles.chatInfo}>
                  <b>{t(ticketTitle(current))}</b>
                  <span>
                    {current.id} · {t(TROUBLE_KINDS[kindOf(current)].label)}
                    {current.bill && <> · <span className="mono">{current.bill}</span>{current.cnee && ` · ${current.cnee}`}</>}
                  </span>
                </span>
                <span className={cx(styles.chip, styles[CHAT_STATUS[current.status].tone])}>{t(CHAT_STATUS[current.status].label)}</span>
                <button type="button" className={styles.headBtn} title={t('Tải đoạn chat (.txt)')} aria-label={t('Tải đoạn chat (.txt)')}
                  onClick={() => downloadTextFile(`${current.id}.txt`, transcriptOf(current, messages, t), 'text/plain;charset=utf-8')}>
                  <Icon name="download" size={14} />
                </button>
              </div>

              <div className={styles.msgs} ref={msgsRef}>
                {messages.map((m, i) =>
                  m.type === 'day' ? (
                    <div key={i} className={styles.day}>{t(dayLabel(m.day, today))}</div>
                  ) : m.type === 'system' ? (
                    <div key={i} className={styles.sys}>{t(m.text, m.params)} · {m.at.slice(11, 16)}</div>
                  ) : (
                    <div key={i} className={cx(styles.msg, m.type === 'me' ? styles.me : styles.cs)}>
                      {m.type === 'cs' && <span className={styles.who}>{t('CS Việt An')}</span>}
                      {m.text}
                      <small>{m.at.slice(11, 16)}</small>
                    </div>
                  )
                )}
              </div>

              {isOpen(current) ? (
                <div className={styles.pending}>
                  <Icon name="message" size={15} />
                  <span>
                    {t('CS trả lời ngay tại đây và trong Thông báo. Cần bổ sung ảnh / thông tin: gọi')}{' '}
                    <a href={CONTACTS[0]!.href}>{CONTACTS[0]!.value}</a> {t('hoặc email')} <a href={CONTACTS[1]!.href}>{CONTACTS[1]!.value}</a>.
                  </span>
                </div>
              ) : (
                <div className={cx(styles.pending, styles.closed)}>
                  <span>{t('Cuộc trò chuyện đã xong. Cần thêm hỗ trợ cho việc này?')}</span>
                  <button type="button" className={styles.linkBtn}
                    onClick={() => setPreset({ kind: kindOf(current), bill: current.bill || undefined })}>
                    {t('Tạo cuộc trò chuyện mới')}
                  </button>
                </div>
              )}
            </>
          )}
        </section>
      </div>

      {preset && (
        <NewConversationDialog
          open
          preset={preset}
          onClose={() => setPreset(null)}
          onSent={ticket => {
            setPreset(null);
            changeFilter('open');
            setCurrentId(ticket.id);
            setChatting(true);
          }}
        />
      )}
    </div>
  );
}
