import { useEffect, useMemo, useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, KeyValueList, Modal, Notice, SelectField, StatusPill, Tabs, TextAreaField } from '@/shared/ui';
import { TROUBLE_PRIORITY, TROUBLE_STATUS, TroubleThread, type TroubleStatus } from '@/features/troubles';
import { useAdminTroubles, useReplyAdminTrouble, type AdminTrouble } from '../api';
import styles from './AdminTroubles.module.css';

type Filter = 'open' | 'all' | TroubleStatus;

const FILTERS: ReadonlyArray<{ key: Filter; label: string }> = [
  { key: 'open', label: 'Chưa xong' },
  { key: 'all', label: 'Tất cả' },
  ...(Object.keys(TROUBLE_STATUS) as TroubleStatus[]).map(key => ({ key, label: TROUBLE_STATUS[key].label }))
];

const matches = (x: AdminTrouble, f: Filter) => (f === 'all' ? true : f === 'open' ? x.ticket.status !== 'done' : x.ticket.status === f);

/** Tab "Sự cố" của trang quản trị: sự cố mọi khách báo, trả lời / đổi trạng thái — khách nhận thông báo riêng trên portal. */
export function AdminTroubles() {
  const { t } = useI18n();
  const list = useAdminTroubles(true);
  const [filter, setFilter] = useState<Filter>('open');
  const [openId, setOpenId] = useState<string | null>(null);
  const items = list.data ?? [];
  const counts = useMemo(() => Object.fromEntries(FILTERS.map(f => [f.key, items.filter(x => matches(x, f.key)).length])), [items]);
  const rows = items.filter(x => matches(x, filter));
  const current = items.find(x => x.ticket.id === openId) ?? null;

  return (
    <Card title={t('Sự cố khách báo ({n})', { n: items.length })} flush>
      <Tabs ariaLabel="Lọc sự cố theo trạng thái" items={FILTERS.map(f => ({ ...f, count: counts[f.key] }))} value={filter} onChange={setFilter} />
      {list.isError ? (
        <div className={styles.pad}><Notice tone="danger">{getErrorMessage(list.error)}</Notice></div>
      ) : list.isLoading ? (
        <p className={`${styles.pad} ${styles.muted}`}>{t('Đang tải…')}</p>
      ) : !rows.length ? (
        <EmptyState title="Không có sự cố" description={items.length ? 'Không có sự cố khớp bộ lọc.' : 'Sự cố khách báo sẽ hiện ở đây.'} />
      ) : (
        <ul className={styles.list}>
          {rows.map(x => (
            <li key={x.ticket.id}>
              <button type="button" className={styles.item} onClick={() => setOpenId(x.ticket.id)}>
                <span className={styles.itemMain}>
                  <span className={styles.itemTitle}>
                    <span className="mono">{x.ticket.id}</span> · {t(x.ticket.type)}
                  </span>
                  <span className={styles.muted}>
                    <span className="mono">{x.ticket.bill}</span> · {x.companyName || x.customerCode} ({x.customerCode})
                  </span>
                  <span className={styles.preview}>{x.ticket.desc}</span>
                </span>
                <span className={styles.itemSide}>
                  <StatusPill tone={TROUBLE_STATUS[x.ticket.status].tone}>{t(TROUBLE_STATUS[x.ticket.status].label)}</StatusPill>
                  <StatusPill tone={TROUBLE_PRIORITY[x.ticket.lv].tone}>{t(TROUBLE_PRIORITY[x.ticket.lv].label)}</StatusPill>
                  <span className={styles.muted}>{x.updatedAt}</span>
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
  const changed = status !== x.status;

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
      title={t('Sự cố {id}', { id: x.id })}
      onClose={onClose}
      footerNote={t('Khách nhận thông báo riêng trên portal khi bạn trả lời hoặc đổi trạng thái')}
      footer={
        <>
          <Button onClick={onClose}>{t('Đóng')}</Button>
          <Button variant="primary" disabled={reply.isPending || (!message.trim() && !changed)} onClick={send}>
            {t(reply.isPending ? 'Đang gửi…' : 'Gửi phản hồi')}
          </Button>
        </>
      }
    >
      <div className={styles.detail}>
        {done && <Notice tone="success">{t(done)}</Notice>}
        {reply.isError && <Notice tone="danger">{getErrorMessage(reply.error)}</Notice>}
        <div className={styles.pills}>
          <StatusPill tone={TROUBLE_STATUS[x.status].tone}>{t(TROUBLE_STATUS[x.status].label)}</StatusPill>
          <StatusPill tone={TROUBLE_PRIORITY[x.lv].tone}>{t(TROUBLE_PRIORITY[x.lv].label)}</StatusPill>
        </div>
        <KeyValueList
          items={[
            ['Khách hàng', `${item.companyName || '—'} · ${item.customerCode}`],
            ['Người báo', t(item.isStaff ? 'tài khoản con {name}' : 'tài khoản chính {name}', { name: item.userName })],
            ['Mã vận đơn', <span className="mono">{x.bill}</span>],
            ['Người nhận', [x.cnee, x.ct].filter(Boolean).join(' · ') || '—'],
            ['Loại sự cố', t(x.type)],
            ['Người yêu cầu', `${x.req}${x.contact ? ` · ${x.contact}` : ''}`],
            ['Ngày gửi', x.date]
          ]}
        />
        <section>
          <h4 className={styles.subTitle}>{t('Mô tả')}</h4>
          <p className={styles.desc}>{x.desc}</p>
        </section>
        <section>
          <h4 className={styles.subTitle}>{t('Trao đổi')}</h4>
          <TroubleThread messages={x.messages ?? []} side="vietan" />
        </section>
        <div className={styles.replyBox}>
          <TextAreaField
            label="Phản hồi cho khách"
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
