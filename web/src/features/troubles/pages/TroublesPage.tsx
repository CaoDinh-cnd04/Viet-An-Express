import { isNotImplemented } from '@/shared/api/http';
import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, PageHeader, StatusPill } from '@/shared/ui';
import { useReplyTrouble, useTroubles } from '../api';
import { ReportTroubleDialog } from '../components/ReportTroubleDialog';
import { TROUBLE_PRIORITY, TROUBLE_STATUS } from '../constants';
import { filterTickets, lastActivity, type TroubleFilter } from '../lib/chat';
import type { TroubleTicket } from '../types';
import styles from './TroublesPage.module.css';

const FILTERS: ReadonlyArray<{ key: TroubleFilter; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'all', label: 'Tất cả' },
  { key: 'done', label: 'Đã xử lý' }
];

/**
 * Quản lý sự cố dạng hộp chat: trái là danh sách sự cố (mới cập nhật trước), phải là trao đổi với CS Việt An.
 * Mở từ đơn hàng bằng /troubles?bill=… sẽ bật sẵn hộp báo sự cố cho đơn đó.
 */
export default function TroublesPage() {
  const { t } = useI18n();
  const { data = [], isLoading, isError, error, refetch } = useTroubles();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<TroubleFilter>('open');
  const [query, setQuery] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Màn hình hẹp: chỉ hiện 1 khung — danh sách hoặc hội thoại
  const [showChat, setShowChat] = useState(false);
  const [reporting, setReporting] = useState(false);
  const billParam = params.get('bill');
  // Mở từ thông báo: /troubles?id=TRB-… → chọn sẵn sự cố đó (lọc Tất cả để thấy cả sự cố đã xử lý)
  const idParam = params.get('id');
  useEffect(() => {
    if (!idParam) return;
    setFilter('all');
    setSelectedId(idParam.toUpperCase());
    setShowChat(true);
    setParams({}, { replace: true });
  }, [idParam, setParams]);

  const list = useMemo(() => filterTickets(data, filter, query), [data, filter, query]);
  const counts = useMemo(() => Object.fromEntries(FILTERS.map(f => [f.key, filterTickets(data, f.key, '').length])), [data]);
  const current = data.find(x => x.id === selectedId) ?? list[0] ?? null;

  const closeReport = () => {
    setReporting(false);
    if (billParam) setParams({}, { replace: true });
  };
  const open = (id: string) => {
    setSelectedId(id);
    setShowChat(true);
  };

  return (
    <>
      <PageHeader
        title="Quản lý sự cố"
        description="Trao đổi trực tiếp với CS Việt An về sự cố của từng đơn."
        actions={<Button variant="primary" size="sm" onClick={() => setReporting(true)}><Icon name="plus" size={15} /> {t('Báo sự cố mới')}</Button>}
      />

      <div className={cx(styles.chat, showChat && styles.chatOpen)}>
        <aside className={styles.side} aria-label={t('Danh sách sự cố')}>
          <div className={styles.search}>
            <Icon name="search" size={15} />
            <input value={query} onChange={e => setQuery(e.target.value)} placeholder={t('Tìm mã sự cố, vận đơn, người nhận…')} aria-label={t('Tìm sự cố')} />
          </div>
          <div className={styles.filters} role="group" aria-label={t('Lọc theo trạng thái')}>
            {FILTERS.map(f => (
              <button key={f.key} type="button" aria-pressed={filter === f.key} className={cx(styles.filter, filter === f.key && styles.filterOn)} onClick={() => setFilter(f.key)}>
                {t(f.label)} <span className={styles.filterCount}>{counts[f.key] ?? 0}</span>
              </button>
            ))}
          </div>
          <ul className={styles.list}>
            {isLoading && <li className={styles.note}>{t('Đang tải…')}</li>}
            {isError && (
              <li className={styles.note}>
                {isNotImplemented(error) ? (
                  t('Chức năng quản lý sự cố đang được kết nối máy chủ. Cần báo gấp, vui lòng gọi hotline Việt An.')
                ) : (
                  <>
                    {t('Không tải được danh sách sự cố')}
                    <Button size="sm" onClick={() => void refetch()}>{t('Thử lại')}</Button>
                  </>
                )}
              </li>
            )}
            {!isLoading && !isError && !list.length && <li className={styles.note}>{t(query ? 'Không có sự cố nào khớp tìm kiếm.' : 'Không có sự cố nào.')}</li>}
            {list.map(x => (
              <li key={x.id}>
                <ConversationItem ticket={x} active={current?.id === x.id} onOpen={() => open(x.id)} />
              </li>
            ))}
          </ul>
        </aside>

        <section className={styles.main} aria-label={t('Trao đổi với CS Việt An')}>
          {current ? (
            <Conversation ticket={current} onBack={() => setShowChat(false)} />
          ) : (
            <div className={styles.placeholder}>
              <Icon name="message" size={34} />
              <p>{t(data.length ? 'Chọn 1 sự cố để xem trao đổi với CS Việt An.' : 'Chưa có sự cố nào. Bấm "Báo sự cố mới" khi đơn hàng cần hỗ trợ.')}</p>
            </div>
          )}
        </section>
      </div>

      <ReportTroubleDialog open={reporting || !!billParam} context={billParam ? { bill: billParam } : null} onClose={closeReport} />
    </>
  );
}

/** 1 dòng trong danh sách: mã + loại sự cố, vận đơn, tin cuối, ngày, trạng thái. */
function ConversationItem({ ticket: x, active, onOpen }: { ticket: TroubleTicket; active: boolean; onOpen: () => void }) {
  const { t } = useI18n();
  const last = lastActivity(x);
  const status = TROUBLE_STATUS[x.status];
  return (
    <button type="button" className={cx(styles.item, active && styles.itemOn)} onClick={onOpen} aria-current={active || undefined}>
      <span className={cx(styles.avatar, styles[status.tone])} aria-hidden="true"><Icon name="alert" size={16} /></span>
      <span className={styles.itemBody}>
        <span className={styles.itemTop}>
          <strong className={styles.itemTitle}>{t(x.type)}</strong>
          <span className={styles.itemDate}>{last.date.split(' ')[0]}</span>
        </span>
        <span className={styles.itemBill}><span className="mono">{x.bill}</span> · {x.id}</span>
        <span className={styles.itemPreview}>
          {last.from === 'vietan' ? `${t('CS Việt An')}: ` : `${t('Bạn')}: `}{last.text}
        </span>
        <span className={cx(styles.itemStatus, styles[status.tone])}>{t(status.label)}</span>
      </span>
    </button>
  );
}

/** Khung hội thoại: đầu khung thông tin sự cố, giữa là tin nhắn, cuối là ô soạn tin. */
function Conversation({ ticket, onBack }: { ticket: TroubleTicket; onBack: () => void }) {
  const { t } = useI18n();
  const reply = useReplyTrouble();
  const [text, setText] = useState('');
  const bodyRef = useRef<HTMLDivElement>(null);
  const done = ticket.status === 'done';
  const messages = ticket.messages ?? [];

  useEffect(() => setText(''), [ticket.id]);
  // Luôn cuộn xuống tin mới nhất khi mở sự cố hoặc có tin mới
  useEffect(() => {
    const el = bodyRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [ticket.id, messages.length]);

  const send = (message: string, status?: TroubleTicket['status']) => {
    if (!message.trim() || reply.isPending) return;
    reply.mutate({ id: ticket.id, reply: message.trim(), status }, { onSuccess: () => setText('') });
  };
  // Enter gửi, Shift+Enter xuống dòng
  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      send(text);
    }
  };

  return (
    <>
      <header className={styles.head}>
        <button type="button" className={styles.back} onClick={onBack} aria-label={t('Quay lại danh sách')}>
          <Icon name="chevronDown" size={18} />
        </button>
        <div className={styles.headInfo}>
          <div className={styles.headTitle}>
            {t(ticket.type)}
            <StatusPill tone={TROUBLE_STATUS[ticket.status].tone}>{TROUBLE_STATUS[ticket.status].label}</StatusPill>
            <StatusPill tone={TROUBLE_PRIORITY[ticket.lv].tone}>{TROUBLE_PRIORITY[ticket.lv].label}</StatusPill>
          </div>
          <div className={styles.headSub}>
            <span className="mono">{ticket.bill}</span> · {ticket.cnee}{ticket.ct ? ` · ${ticket.ct}` : ''} · {ticket.id}
          </div>
        </div>
        {!done && (
          <div className={styles.headActions}>
            <Button size="sm" onClick={() => send('Khách hàng nhắc CS xử lý yêu cầu.')} disabled={reply.isPending}>{t('Nhắc CS')}</Button>
            <Button size="sm" variant="ghost" onClick={() => send('Khách hàng xác nhận đã xử lý xong.', 'done')} disabled={reply.isPending}>
              <Icon name="check" size={14} /> {t('Đã xử lý xong')}
            </Button>
          </div>
        )}
      </header>

      <div className={styles.body} ref={bodyRef}>
        <div className={styles.dayNote}>{t('Bạn báo sự cố lúc {date}', { date: ticket.date })}</div>
        <Bubble mine author={ticket.req || t('Bạn')} date={ticket.date} text={ticket.desc} />
        {messages.map((m, i) => (
          <div key={i} className={styles.turn}>
            {m.message.trim() && (
              <Bubble mine={m.from === 'customer'} author={m.from === 'vietan' ? t('CS Việt An') : m.author} date={m.date} text={m.message} />
            )}
            {m.status && (
              <div className={styles.system}>
                {t('Chuyển trạng thái: {status}', { status: t(TROUBLE_STATUS[m.status]?.label ?? m.status) })}
              </div>
            )}
          </div>
        ))}
        {!messages.some(m => m.from === 'vietan') && <div className={styles.system}>{t('CS Việt An sẽ phản hồi trong giờ làm việc.')}</div>}
      </div>

      {done ? (
        <div className={styles.closed}>{t('Sự cố đã xử lý xong. Cần hỗ trợ thêm, vui lòng báo sự cố mới.')}</div>
      ) : (
        <div className={styles.composer}>
          <textarea
            rows={1}
            value={text}
            onChange={e => setText(e.target.value)}
            onKeyDown={onKeyDown}
            placeholder={t('Nhập tin nhắn cho CS Việt An… (Enter để gửi, Shift+Enter xuống dòng)')}
            aria-label={t('Tin nhắn')}
          />
          <button type="button" className={styles.send} onClick={() => send(text)} disabled={reply.isPending || !text.trim()} aria-label={t('Gửi tin nhắn')}>
            <Icon name="send" size={17} />
          </button>
        </div>
      )}
    </>
  );
}

function Bubble({ mine, author, date, text }: { mine: boolean; author: string; date: string; text: string }) {
  return (
    <div className={cx(styles.msg, mine && styles.mine)}>
      {!mine && <span className={styles.csAvatar} aria-hidden="true">VA</span>}
      <div className={styles.msgCol}>
        <div className={styles.msgMeta}>{author} · {date}</div>
        <div className={styles.bubble}>{text}</div>
      </div>
    </div>
  );
}
