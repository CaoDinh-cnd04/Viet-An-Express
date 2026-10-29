import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Drawer, Icon, PageHeader } from '@/shared/ui';
import { useMarkAllRead, useMarkRead, useNotifications, type Notification } from '../api';
import { FilterBar, type FilterOption } from '../components/FilterBar';
import { NoticeContent } from '../components/NoticeContent';
import { NoticeRow } from '../components/NoticeRow';
import { filterCounts, filterInbox, groupByDay, partsOf, ticketIdOf, type InboxFilter } from '../lib/inbox';
import styles from './NotificationsPage.module.css';

/**
 * Thông báo của khách: lọc Tất cả / Chưa đọc / Quan trọng / Hỗ trợ & sự cố, tìm kiếm, nhóm Đã ghim / theo ngày.
 * Bấm 1 thông báo → khung chi tiết trượt từ phải (đánh dấu đã đọc); cập nhật hỗ trợ có mã TRB → mở đúng yêu cầu.
 * /notifications?id=… (từ chuông) mở sẵn thông báo đó.
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
  const [openId, setOpenId] = useState<string | null>(null);
  const all = useMemo(() => data?.data ?? [], [data]);
  const today = new Date();

  const counts = useMemo(() => filterCounts(all), [all]);
  const groups = useMemo(() => groupByDay(filterInbox(all, filter, query), new Date()), [all, filter, query]);
  const current = all.find(n => String(n.id) === openId) ?? null;

  const options: ReadonlyArray<FilterOption<InboxFilter>> = [
    { key: 'all', label: 'Tất cả', count: counts.all },
    { key: 'unread', label: 'Chưa đọc', count: counts.unread, hot: true },
    { key: 'important', label: 'Quan trọng', count: counts.important },
    { key: 'support', label: 'Hỗ trợ & sự cố', count: counts.support, hot: true }
  ];

  const open = (n: Notification) => {
    if (n.unread) markRead.mutate(n.id);
    const ticketId = n.kind === 'support' ? ticketIdOf(n.title) : null;
    if (ticketId) void navigate(`/troubles?id=${encodeURIComponent(ticketId)}`);
    else setOpenId(String(n.id));
  };

  // Mở từ chuông: /notifications?id=…
  const idParam = params.get('id');
  useEffect(() => {
    if (!idParam || !data) return;
    const found = data.data.find(n => String(n.id) === idParam);
    if (found) {
      setFilter(found.kind === 'support' ? 'support' : 'all');
      setOpenId(String(found.id));
    }
    setParams({}, { replace: true });
  }, [idParam, data, setParams]);

  return (
    <>
      <PageHeader
        title="Thông báo"
        description="Tin từ Việt An và cập nhật yêu cầu hỗ trợ của bạn"
        actions={
          <Button variant="ghost" onClick={() => markAll.mutate()} disabled={!data?.unreadCount || markAll.isPending}>
            <Icon name="check" size={15} /> {t('Đánh dấu đã đọc hết')}
          </Button>
        }
      />

      <FilterBar options={options} value={filter} onChange={setFilter} query={query} onQuery={setQuery} />

      <div className={styles.card}>
        {isLoading && <p className={styles.note}>{t('Đang tải…')}</p>}
        {isError && (
          <div className={styles.note}>
            {t(isNotImplemented(error) ? 'Chức năng thông báo đang được kết nối máy chủ' : 'Không tải được thông báo')}
            {!isNotImplemented(error) && <Button size="sm" onClick={() => void refetch()}>{t('Thử lại')}</Button>}
          </div>
        )}
        {!isLoading && !isError && !groups.length && (
          <p className={styles.note}>{t(query || filter !== 'all' ? 'Không có thông báo nào khớp.' : 'Chưa có thông báo')}</p>
        )}
        {groups.map(g => (
          <section key={g.label}>
            <h3 className={styles.group}>{t(g.label)}</h3>
            {g.items.map(n => (
              <NoticeRow key={n.id} notice={n} today={today} active={String(n.id) === openId} onOpen={open} />
            ))}
          </section>
        ))}
      </div>

      <Drawer open={!!current} title="Thông báo" onClose={() => setOpenId(null)}>
        {current && (
          <NoticeContent title={current.title} meta={`${t('Việt An Express')} · ${current.date}`} imp={current.imp} pin={current.pin} parts={partsOf(current)} />
        )}
      </Drawer>
    </>
  );
}
