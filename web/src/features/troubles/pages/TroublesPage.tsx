import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { PageHeader } from '@/shared/ui';
import { useTroubles } from '../api';
import { TroubleForm } from '../components/TroubleForm';
import { CONTACTS, SENDER_INFO_TOPIC, TROUBLE_KIND_ORDER, TROUBLE_KINDS, TROUBLE_PRIORITY, TROUBLE_STATUS } from '../constants';
import { filterTickets, kindOf, ticketSubline, ticketTitle, type TroubleFilter } from '../lib/chat';
import type { TroubleKind, TroubleTicket } from '../types';
import ws from '@/shared/ui/Workspace.module.css';
import styles from './TroublesPage.module.css';

const FILTERS: ReadonlyArray<{ key: TroubleFilter; label: string }> = [
  { key: 'all', label: 'Tất cả' },
  { key: 'open', label: 'Đang xử lý' },
  { key: 'done', label: 'Đã xử lý' }
];

/** Nhãn menu gửi yêu cầu — lời khách hay dùng. */
const SEND_LABELS: Record<TroubleKind, string> = {
  su_co: 'Báo sự cố đơn hàng',
  ho_tro: 'Yêu cầu hỗ trợ',
  gop_y: 'Góp ý'
};

type View =
  | { kind: 'list' }
  | { kind: 'detail'; id: string }
  | { kind: 'form'; type: TroubleKind; bill?: string; topic?: string };

/**
 * Hỗ trợ & sự cố kiểu phần mềm nghiệp vụ: hàng trên có tab lọc (Tất cả / Đang xử lý / Đã xử lý) và nút gửi 3 loại yêu cầu,
 * khung dưới hiển thị bảng, chi tiết hoặc form gửi; liên hệ ở footer. CS xử lý ở phần mềm cũ, báo kết quả qua Thông báo.
 * /troubles?bill=… mở form báo sự cố cho đơn đó; ?new=sender-info mở form hỗ trợ "Sửa thông tin người gửi"; ?id=TRB-… mở chi tiết.
 */
export default function TroublesPage() {
  const { t } = useI18n();
  const { data = [], isLoading, isError, error } = useTroubles();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<TroubleFilter>('all');
  const [query, setQuery] = useState('');
  const [view, setView] = useState<View>({ kind: 'list' });

  // Link từ đơn hàng / form tạo đơn / thông báo
  useEffect(() => {
    const bill = params.get('bill');
    const id = params.get('id');
    const isNew = params.get('new') === 'sender-info';
    if (!bill && !id && !isNew) return;
    if (bill) setView({ kind: 'form', type: 'su_co', bill });
    else if (isNew) setView({ kind: 'form', type: 'ho_tro', topic: SENDER_INFO_TOPIC });
    else if (id) setView({ kind: 'detail', id: id.toUpperCase() });
    setParams({}, { replace: true });
  }, [params, setParams]);

  const rows = useMemo(() => filterTickets(data, filter, query), [data, filter, query]);
  const counts = useMemo(() => Object.fromEntries(FILTERS.map(f => [f.key, filterTickets(data, f.key, '').length])), [data]);
  const current = view.kind === 'detail' ? data.find(x => x.id === view.id) ?? null : null;
  const showList = (f: TroubleFilter = filter) => {
    setFilter(f);
    setView({ kind: 'list' });
  };

  const isList = view.kind === 'list';

  return (
    <>
      <PageHeader title="Hỗ trợ & sự cố" description="Gửi yêu cầu cho CS Việt An và theo dõi kết quả." />

      {/* Chức năng (tab lọc) bên trái · nút thao tác bên phải */}
      <div className={ws.toolbar}>
        <div className={ws.tabs} role="tablist" aria-label={t('Yêu cầu đã gửi')}>
          {FILTERS.map(f => (
            <button
              key={f.key}
              type="button"
              role="tab"
              aria-selected={isList && filter === f.key}
              className={cx(ws.tab, isList && filter === f.key && ws.tabOn)}
              onClick={() => showList(f.key)}
            >
              {t(f.label)} <span className={ws.count}>{counts[f.key] ?? 0}</span>
            </button>
          ))}
        </div>
        <div className={ws.tools}>
          {isList && (
            <input className={ws.search} value={query} onChange={e => setQuery(e.target.value)} placeholder={t('Tìm mã yêu cầu, vận đơn, tiêu đề…')} aria-label={t('Tìm yêu cầu')} />
          )}
          {/* Nút đang mở form được tô màu để khách thấy rõ mình đang gửi loại nào */}
          {TROUBLE_KIND_ORDER.map(k => {
            const on = view.kind === 'form' && view.type === k;
            return (
              <button key={k} type="button" aria-pressed={on} className={on ? ws.btn : ws.btnSecondary} onClick={() => setView({ kind: 'form', type: k })}>
                {t(SEND_LABELS[k])}
              </button>
            );
          })}
        </div>
      </div>

      {/* Hiển thị: bảng / chi tiết / form */}
      <section className={ws.box}>
        {view.kind === 'form' ? (
          <>
            <div className={ws.boxHead}><b>{t(SEND_LABELS[view.type])}</b></div>
            <TroubleForm
              key={`${view.type}-${view.bill ?? ''}-${view.topic ?? ''}`}
              kind={view.type}
              bill={view.bill}
              topic={view.topic}
              onSent={ticket => setView({ kind: 'detail', id: ticket.id })}
              onCancel={() => showList()}
            />
          </>
        ) : view.kind === 'detail' ? (
          <>
            <div className={ws.boxHead}>
              <b>{current ? `${current.id} · ${t(ticketTitle(current))}` : view.id}</b>
              <button type="button" className={ws.btnSecondary} onClick={() => showList()}>← {t('Quay lại danh sách')}</button>
            </div>
            {current ? <Detail ticket={current} /> : <p className={ws.note}>{t(isLoading ? 'Đang tải…' : 'Không tìm thấy yêu cầu')}</p>}
          </>
        ) : isLoading ? (
          <p className={ws.note}>{t('Đang tải…')}</p>
        ) : isError ? (
          <p className={ws.note}>
            {t(isNotImplemented(error)
              ? 'Chức năng hỗ trợ & sự cố đang được kết nối máy chủ. Cần báo gấp, vui lòng gọi hotline Việt An.'
              : 'Không tải được danh sách yêu cầu')}
          </p>
        ) : !rows.length ? (
          <p className={ws.note}>
            {t(data.length ? 'Không có yêu cầu nào khớp.' : 'Chưa có yêu cầu nào. Bấm "Báo sự cố đơn hàng", "Yêu cầu hỗ trợ" hoặc "Góp ý" phía trên khi cần.')}
          </p>
        ) : (
          <div className={ws.tableWrap}>
            <table className={ws.table}>
              <thead>
                <tr>
                  <th style={{ width: 80 }}>{t('Mã')}</th>
                  <th style={{ width: 120 }}>{t('Loại')}</th>
                  <th>{t('Nội dung')}</th>
                  <th style={{ width: 160 }}>{t('Vận đơn / chủ đề')}</th>
                  <th style={{ width: 140 }}>{t('Trạng thái')}</th>
                  <th style={{ width: 100 }}>{t('Cập nhật')}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(x => (
                  <tr key={x.id} className={ws.rowLink} tabIndex={0} onClick={() => setView({ kind: 'detail', id: x.id })} onKeyDown={e => e.key === 'Enter' && setView({ kind: 'detail', id: x.id })}>
                    <td className="mono">{x.id}</td>
                    <td>{t(TROUBLE_KINDS[kindOf(x)].label)}</td>
                    <td>{t(ticketTitle(x))}</td>
                    <td className={kindOf(x) === 'su_co' ? 'mono' : undefined}>{kindOf(x) === 'su_co' ? ticketSubline(x) : t(ticketSubline(x))}</td>
                    <td className={cx(styles.status, styles[`st_${x.status}`])}>{t(TROUBLE_STATUS[x.status].label)}</td>
                    <td>{(x.updatedAt ?? x.date).split(' ')[0]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Liên hệ */}
      <footer className={ws.footer}>
        {CONTACTS.map(c => (
          <span key={c.label}>{t(c.label)}: <b>{c.value}</b></span>
        ))}
        <span>{t('CS báo kết quả qua mục Thông báo, điện thoại hoặc email.')}</span>
      </footer>
    </>
  );
}

/** Chi tiết 1 yêu cầu: bảng thông tin + nội dung khách gửi. */
function Detail({ ticket: x }: { ticket: TroubleTicket }) {
  const { t } = useI18n();
  const kind = kindOf(x);
  const rows: Array<[string, string]> = [
    ['Trạng thái', `${t(TROUBLE_STATUS[x.status].label)}${x.updatedAt ? ` (${x.updatedAt})` : ''}`],
    ['Loại', t(TROUBLE_KINDS[kind].label) + (kind === 'su_co' ? ` · ${t('Mức độ')}: ${t(TROUBLE_PRIORITY[x.lv].label)}` : '')]
  ];
  if (kind === 'su_co') {
    rows.push(['Vận đơn', x.bill], ['Vấn đề', t(x.type)]);
    if (x.cnee || x.ct) rows.push(['Người nhận', [x.cnee, x.ct].filter(Boolean).join(' · ')]);
  } else if (kind === 'ho_tro') {
    rows.push(['Chủ đề', t(x.type)], ['Tiêu đề', x.subject ?? '']);
  }
  rows.push(['Người yêu cầu', [x.req, x.contact].filter(Boolean).join(' · ')], ['Ngày gửi', x.date]);

  return (
    <div className={styles.detail}>
      <dl>
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{t(label)}</dt>
            <dd className={cx(label === 'Trạng thái' && styles.status, label === 'Trạng thái' && styles[`st_${x.status}`], label === 'Vận đơn' && 'mono')}>{value}</dd>
          </div>
        ))}
      </dl>
      <p className={styles.content}>{x.desc}</p>
    </div>
  );
}
