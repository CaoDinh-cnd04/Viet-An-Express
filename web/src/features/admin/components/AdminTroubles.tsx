import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, KeyValueList, Modal, Notice, SelectField, StatusPill, Tabs, TextAreaField } from '@/shared/ui';
import { kindOf, ticketSubline, ticketTitle, TROUBLE_KINDS, TROUBLE_PRIORITY, TROUBLE_STATUS, type TroubleKind, type TroubleStatus } from '@/features/troubles';
import { useAdminTroubles, useReplyAdminTrouble, type AdminTrouble } from '../api';
import styles from './AdminTroubles.module.css';

type Filter = 'open' | 'all' | TroubleStatus;
type KindFilter = 'all' | TroubleKind;

const KIND_FILTERS: ReadonlyArray<{ value: KindFilter; label: string }> = [
  { value: 'all', label: 'Mọi loại' },
  ...(Object.keys(TROUBLE_KINDS) as TroubleKind[]).map(k => ({ value: k, label: TROUBLE_KINDS[k].label }))
];

const FILTERS: ReadonlyArray<{ key: Filter; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'all', label: 'Tất cả' },
  ...(Object.keys(TROUBLE_STATUS) as TroubleStatus[]).map(key => ({ key, label: TROUBLE_STATUS[key].label }))
];

const matches = (x: AdminTrouble, f: Filter) => (f === 'all' ? true : f === 'open' ? x.ticket.status !== 'done' : x.ticket.status === f);

/** Tab "Hỗ trợ & sự cố" của trang quản trị: sự cố, yêu cầu hỗ trợ, góp ý của mọi khách — trả lời / đổi trạng thái, khách nhận thông báo riêng. */
export function AdminTroubles() {
  const { t } = useI18n();
  const list = useAdminTroubles(true);
  const [filter, setFilter] = useState<Filter>('open');
  const [kind, setKind] = useState<KindFilter>('all');
  const [openId, setOpenId] = useState<string | null>(null);
  const items = (list.data ?? []).filter(x => kind === 'all' || kindOf(x.ticket) === kind);
  const counts = useMemo(() => Object.fromEntries(FILTERS.map(f => [f.key, items.filter(x => matches(x, f.key)).length])), [items]);
  const rows = items.filter(x => matches(x, filter));
  const current = items.find(x => x.ticket.id === openId) ?? null;

  return (
    <Card
      title={t('Yêu cầu của khách ({n})', { n: items.length })}
      actions={
        <select className={styles.kindSelect} aria-label={t('Lọc theo loại yêu cầu')} value={kind} onChange={e => setKind(e.target.value as KindFilter)}>
          {KIND_FILTERS.map(k => <option key={k.value} value={k.value}>{t(k.label)}</option>)}
        </select>
      }
      flush
    >
      <Tabs ariaLabel="Lọc sự cố theo trạng thái" items={FILTERS.map(f => ({ ...f, count: counts[f.key] }))} value={filter} onChange={setFilter} />
      {list.isError ? (
        <div className={styles.pad}><Notice tone="danger">{getErrorMessage(list.error)}</Notice></div>
      ) : list.isLoading ? (
        <p className={`${styles.pad} ${styles.muted}`}>{t('Đang tải…')}</p>
      ) : !rows.length ? (
        <EmptyState title="Không có yêu cầu" description={items.length ? 'Không có yêu cầu khớp bộ lọc.' : 'Sự cố, yêu cầu hỗ trợ và góp ý của khách sẽ hiện ở đây.'} />
      ) : (
        <ul className={styles.list}>
          {rows.map(x => (
            <li key={x.ticket.id}>
              <button type="button" className={styles.item} onClick={() => setOpenId(x.ticket.id)}>
                <span className={styles.itemMain}>
                  <span className={styles.itemTitle}>
                    <span className="mono">{x.ticket.id}</span> · {t(ticketTitle(x.ticket))}
                  </span>
                  <span className={styles.muted}>
                    {t(TROUBLE_KINDS[kindOf(x.ticket)].label)}
                    {ticketSubline(x.ticket) && <> · {kindOf(x.ticket) === 'su_co' ? <span className="mono">{ticketSubline(x.ticket)}</span> : t(ticketSubline(x.ticket))}</>}
                    {' '}· {x.companyName || x.customerCode} ({x.customerCode})
                  </span>
                  <span className={styles.preview}>{x.ticket.desc}</span>
                </span>
                <span className={styles.itemSide}>
                  <StatusPill tone={TROUBLE_STATUS[x.ticket.status].tone}>{t(TROUBLE_STATUS[x.ticket.status].label)}</StatusPill>
                  {kindOf(x.ticket) === 'su_co' && <StatusPill tone={TROUBLE_PRIORITY[x.ticket.lv].tone}>{t(TROUBLE_PRIORITY[x.ticket.lv].label)}</StatusPill>}
                  <span className={styles.muted}>{x.ticket.updatedAt ?? x.ticket.date}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <AdminTroubleDialog item={current} onClose={() => setOpenId(null)} />
    </Card>
  );
}

const STATUS_OPTIONS = (Object.keys(TROUBLE_STATUS) as TroubleStatus[]).map(k => ({ value: k, label: TROUBLE_STATUS[k].label }));

function AdminTroubleDialog({ item, onClose }: { item: AdminTrouble | null; onClose: () => void }) {
  const { t } = useI18n();
  const reply = useReplyAdminTrouble();
  const [message, setMessage] = useState('');
  const [status, setStatus] = useState<TroubleStatus>('doing');
  const [done, setDone] = useState<string | null>(null);
  const id = item?.ticket.id;

  // Mở sự cố khác: trạng thái gợi ý = "Đang xử lý" nếu sự cố còn mới, không thì giữ trạng thái hiện tại.
  useEffect(() => {
    setMessage('');
    setDone(null);
    reply.reset();
    if (item) setStatus(item.ticket.status === 'new' ? 'doing' : item.ticket.status);
  }, [id]);
  if (!item) return null;
  const x = item.ticket;
  const kind = kindOf(x);
  const changed = status !== x.status;
  const info: Array<[string, ReactNode]> = [
    ['Khách hàng', `${item.companyName || '—'} · ${item.customerCode}`],
    ['Người gửi', t(item.isStaff ? 'tài khoản con {name}' : 'tài khoản chính {name}', { name: x.req })],
    ['Loại yêu cầu', t(TROUBLE_KINDS[kind].label)]
  ];
  if (kind === 'su_co') {
    info.push(['Mã vận đơn', <span className="mono">{x.bill}</span>], ['Người nhận', [x.cnee, x.ct].filter(Boolean).join(' · ') || '—'], ['Loại sự cố', t(x.type)]);
  } else if (kind === 'ho_tro') {
    info.push(['Tiêu đề', x.subject || '—'], ['Chủ đề', t(x.type)]);
  }
  info.push(['Người yêu cầu', `${x.req}${x.contact ? ` · ${x.contact}` : ''}`], ['Ngày gửi', x.date]);

  const send = () =>
    reply.mutate(
      { id: x.id, reply: message, status: changed ? status : undefined },
      {
        onSuccess: res => {
          setMessage('');
          setDone(res.message);
        }
      }
    );

  return (
    <Modal
      open
      size="lg"
      title={`${t(TROUBLE_KINDS[kind].label)} ${x.id}`}
      onClose={onClose}
      footerNote={t('Khách nhận thông báo riêng trên portal kèm ghi chú; xử lý chi tiết ở phần mềm cũ')}
      footer={
        <>
          <Button onClick={onClose}>{t('Đóng')}</Button>
          <Button variant="primary" disabled={reply.isPending || (!message.trim() && !changed)} onClick={send}>
            {t(reply.isPending ? 'Đang gửi…' : 'Cập nhật & báo khách')}
          </Button>
        </>
      }
    >
      <div className={styles.detail}>
        {done && <Notice tone="success">{t(done)}</Notice>}
        {reply.isError && <Notice tone="danger">{getErrorMessage(reply.error)}</Notice>}
        <div className={styles.pills}>
          <StatusPill tone={TROUBLE_STATUS[x.status].tone}>{t(TROUBLE_STATUS[x.status].label)}</StatusPill>
          {kind === 'su_co' && <StatusPill tone={TROUBLE_PRIORITY[x.lv].tone}>{t(TROUBLE_PRIORITY[x.lv].label)}</StatusPill>}
        </div>
        <KeyValueList items={info} />
        <section>
          <h4 className={styles.subTitle}>{t('Nội dung')}</h4>
          <p className={styles.desc}>{x.desc}</p>
        </section>
        <div className={styles.replyBox}>
          <TextAreaField
            label="Ghi chú gửi khách (không bắt buộc)"
            rows={4}
            maxLength={4000}
            placeholder={t('Kết quả kiểm tra, hướng xử lý, thông tin cần khách bổ sung…')}
            value={message}
            onChange={e => setMessage(e.target.value)}
          />
          <SelectField
            label="Trạng thái"
            options={STATUS_OPTIONS}
            value={status}
            hint={changed ? t('Sẽ đổi từ "{from}"', { from: t(TROUBLE_STATUS[x.status].label) }) : undefined}
            onChange={e => setStatus(e.target.value as TroubleStatus)}
          />
        </div>
      </div>
    </Modal>
  );
}
