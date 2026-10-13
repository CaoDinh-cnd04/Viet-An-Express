import { isNotImplemented } from '@/shared/api/http';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, ExternalLinkButton, Icon, PageHeader } from '@/shared/ui';
import { useMarkAllRead, useMarkRead, useNotifications, type Notification } from '../api';
import { filterInbox, groupByDay, noticeKind, troubleIdOf, type InboxFilter, type NoticeKind } from '../lib/inbox';
import { safeLink } from '../lib/link';
import styles from './NotificationsPage.module.css';

const FILTERS: ReadonlyArray<{ key: InboxFilter; label: string }> = [
  { key: 'all', label: 'Tất cả' },
  { key: 'unread', label: 'Chưa đọc' },
  { key: 'important', label: 'Quan trọng' },
  { key: 'trouble', label: 'Sự cố' }
];

const KIND_ICON: Record<NoticeKind, 'alert' | 'bell' | 'message'> = { trouble: 'message', important: 'alert', general: 'bell' };
const KIND_LABEL: Record<NoticeKind, string> = { trouble: 'Sự cố', important: 'Quan trọng', general: 'Thông báo' };

/**
 * Trung tâm thông báo dạng hộp thư: trái là danh sách (lọc Tất cả / Chưa đọc / Quan trọng / Sự cố, tìm kiếm, nhóm theo ngày),
 * phải là nội dung thông báo đang chọn. Mở 1 thông báo = đánh dấu đã đọc. /notifications?id=… (từ popup chuông) mở sẵn thông báo đó.
 */
export default function NotificationsPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { data, isLoading, isError, error, refetch } = useNotifications();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const [params, setParams] = useSearchParams();
  const [filter, setFilter] = useState<InboxFilter>('all');
  const [query, setQuery] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Màn hình hẹp: chỉ hiện 1 khung — danh sách hoặc nội dung
  const [showDetail, setShowDetail] = useState(false);
  const all = data?.data ?? [];

  const list = useMemo(() => filterInbox(all, filter, query), [all, filter, query]);
  const groups = useMemo(() => groupByDay(list, new Date()), [list]);
  const counts = useMemo(() => Object.fromEntries(FILTERS.map(f => [f.key, filterInbox(all, f.key, '').length])), [all]);
  const current = all.find(n => String(n.id) === selectedId) ?? null;

  const select = (n: Notification) => {
    setSelectedId(String(n.id));
    setShowDetail(true);
    if (n.unread) markRead.mutate(n.id);
  };

  // Mở từ popup chuông: /notifications?id=…
  const idParam = params.get('id');
  useEffect(() => {
    if (!idParam || !data) return;
    const found = data.data.find(n => String(n.id) === idParam);
    if (found) {
      setSelectedId(String(found.id));
      setShowDetail(true);
    }
    setParams({}, { replace: true });
  }, [idParam, data, setParams]);

  // Chưa chọn gì → mở sẵn thông báo đầu danh sách (màn hình rộng)
  useEffect(() => {
    if (!selectedId && list[0]) setSelectedId(String(list[0].id));
  }, [selectedId, list]);

  return (
    <>
      <PageHeader title="Trung tâm thông báo" description="Thông báo từ Việt An: lịch cut-off, phụ phí, quy định hãng bay, bảo trì hệ thống, cập nhật sự cố." />

      <div className={cx(styles.inbox, showDetail && styles.inboxDetail)}>
        <aside className={styles.side} aria-label={t('Danh sách thông báo')}>
          <div className={styles.sideHead}>
            <div className={styles.search}>
              <Icon name="search" size={15} />
              <input value={query} onChange={e => setQuery(e.target.value)} placeholder={t('Tìm thông báo…')} aria-label={t('Tìm thông báo')} />
            </div>
            <button type="button" className={styles.markAll} onClick={() => markAll.mutate()} disabled={!data?.unreadCount || markAll.isPending} title={t('Đánh dấu đã đọc tất cả')}>
              <Icon name="check" size={15} />
            </button>
          </div>
          <div className={styles.filters} role="group" aria-label={t('Lọc thông báo')}>
            {FILTERS.map(f => (
              <button key={f.key} type="button" aria-pressed={filter === f.key} className={cx(styles.filter, filter === f.key && styles.filterOn)} onClick={() => setFilter(f.key)}>
                {t(f.label)}
                {(counts[f.key] ?? 0) > 0 && <span className={cx(styles.filterCount, f.key === 'unread' && styles.filterCountHot)}>{counts[f.key]}</span>}
              </button>
            ))}
          </div>

          <div className={styles.list}>
            {isLoading && <p className={styles.note}>{t('Đang tải…')}</p>}
            {isError && (
              <div className={styles.note}>
                {isNotImplemented(error) ? t('Chức năng thông báo đang được kết nối máy chủ') : t('Không tải được thông báo')}
                {!isNotImplemented(error) && <Button size="sm" onClick={() => void refetch()}>{t('Thử lại')}</Button>}
              </div>
            )}
            {!isLoading && !isError && !list.length && <p className={styles.note}>{t(query || filter !== 'all' ? 'Không có thông báo nào khớp.' : 'Chưa có thông báo')}</p>}
            {groups.map(g => (
              <section key={g.label}>
                <h3 className={styles.day}>{t(g.label)}</h3>
                <ul className={styles.items}>
                  {g.items.map(n => {
                    const kind = noticeKind(n);
                    return (
                      <li key={n.id}>
                        <button type="button" className={cx(styles.item, n.unread && styles.unread, current?.id === n.id && styles.itemOn)} onClick={() => select(n)} aria-current={current?.id === n.id || undefined}>
                          <span className={cx(styles.icon, styles[kind])} aria-hidden="true"><Icon name={KIND_ICON[kind]} size={16} /></span>
                          <span className={styles.text}>
                            <span className={styles.title}>{n.title}</span>
                            <span className={styles.snippet}>{n.body.split('\n')[0]}</span>
                          </span>
                          <span className={styles.time}>
                            {n.date.slice(11, 16)}
                            {n.unread && <span className={styles.dot} aria-label={t('Chưa đọc')} />}
                          </span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              </section>
            ))}
          </div>
        </aside>

        <section className={styles.main} aria-label={t('Nội dung thông báo')}>
          {current ? (
            <NoticeDetail notice={current} onBack={() => setShowDetail(false)} onOpenTrouble={id => void navigate(`/troubles?id=${encodeURIComponent(id)}`)} />
          ) : (
            <div className={styles.placeholder}>
              <Icon name="bell" size={34} />
              <p>{t(all.length ? 'Chọn 1 thông báo để xem nội dung.' : 'Chưa có thông báo')}</p>
            </div>
          )}
        </section>
      </div>
    </>
  );
}

function NoticeDetail({ notice: n, onBack, onOpenTrouble }: { notice: Notification; onBack: () => void; onOpenTrouble: (id: string) => void }) {
  const { t } = useI18n();
  const kind = noticeKind(n);
  const link = safeLink(n.link);
  const trouble = kind === 'trouble' ? troubleIdOf(n.title) : null;
  return (
    <article className={styles.detail}>
      <button type="button" className={styles.back} onClick={onBack}>
        <Icon name="chevronDown" size={16} /> {t('Quay lại danh sách')}
      </button>
      <div className={styles.detailHead}>
        <span className={cx(styles.icon, styles.iconLg, styles[kind])} aria-hidden="true"><Icon name={KIND_ICON[kind]} size={20} /></span>
        <div>
          <span className={cx(styles.kind, styles[kind])}>{t(KIND_LABEL[kind])}</span>
          <h2 className={styles.detailTitle}>{n.title}</h2>
          <p className={styles.meta}><Icon name="clock" size={13} /> {n.date} · {t('Việt An Express')}</p>
        </div>
      </div>
      <div className={styles.body}>{n.body}</div>
      {(link || trouble) && (
        <div className={styles.actions}>
          {trouble && (
            <Button variant="primary" onClick={() => onOpenTrouble(trouble)}>
              <Icon name="message" size={15} /> {t('Mở sự cố {id}', { id: trouble })}
            </Button>
          )}
          {link && (
            <ExternalLinkButton variant={trouble ? 'secondary' : 'primary'} href={link}>
              {t('Xem tại đây')} <Icon name="arrowRight" size={15} />
            </ExternalLinkButton>
          )}
        </div>
      )}
    </article>
  );
}
