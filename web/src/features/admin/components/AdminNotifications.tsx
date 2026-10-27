import { useMemo, useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { stripDiacritics } from '@/shared/lib/text';
import { cx } from '@/shared/lib/cx';
import { Button, Drawer, Icon, Notice, StatCard, StatGrid, useToast } from '@/shared/ui';
import { FilterBar, NoticeContent, notificationImageUrl, type FilterOption } from '@/features/notifications';
import {
  useAdminNotifications, useDeleteNotification, useNotificationAudience, useNotificationReaders, useRemoveNotification, type AdminNotification
} from '../api';
import { NotificationEditor } from './NotificationEditor';
import styles from './AdminNotifications.module.css';

type Status = AdminNotification['status'];

const STATUS_LABEL: Record<Status, string> = {
  live: 'Đang hiện', scheduled: 'Hẹn giờ', draft: 'Nháp', expired: 'Hết hạn', removed: 'Đã gỡ'
};

const EMAIL_LABEL: Record<AdminNotification['emailStatus'], string> = {
  none: 'Không gửi email',
  scheduled: 'Email gửi khi đăng',
  sending: 'Đang gửi email…',
  sent: 'Đã gửi email',
  partial: 'Gửi email một phần',
  failed: 'Gửi email lỗi',
  no_recipients: 'Không có khách nào có email'
};

const norm = (s: string) => stripDiacritics(s).toLowerCase();
/** 'yyyy-MM-ddTHH:mm' → 'dd/MM/yyyy HH:mm' */
const showTime = (v: string) => `${v.slice(8, 10)}/${v.slice(5, 7)}/${v.slice(0, 4)} ${v.slice(11, 16)}`;

/**
 * Tab "Thông báo" của trang quản trị: số liệu, lọc theo trạng thái, danh sách (người nhận, % đã đọc, email),
 * bấm 1 thông báo → khung chi tiết (nội dung như khách thấy + khách đã đọc + Sửa / Gỡ / Xóa). "Soạn thông báo" mở trình soạn toàn trang.
 */
export function AdminNotifications() {
  const { t } = useI18n();
  const toast = useToast();
  const list = useAdminNotifications(true);
  const audience = useNotificationAudience();
  const remove = useRemoveNotification();
  const del = useDeleteNotification();
  const [filter, setFilter] = useState<Status>('live');
  const [query, setQuery] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);
  const [editing, setEditing] = useState<AdminNotification | 'new' | { copy: AdminNotification } | null>(null);

  const rows = useMemo(() => list.data ?? [], [list.data]);
  const current = rows.find(n => n.id === openId) ?? null;
  const shown = useMemo(() => {
    const q = norm(query.trim());
    return rows
      .filter(n => n.status === filter)
      .filter(n => !q || norm([n.title, ...n.items.flatMap(i => [i.title ?? '', i.body])].join(' ')).includes(q));
  }, [rows, filter, query]);

  const count = (s: Status) => rows.filter(n => n.status === s).length;
  const live = rows.filter(n => n.status === 'live');
  const readRate = live.length ? Math.round((live.reduce((sum, n) => sum + (n.recipients ? n.readCustomers / n.recipients : 0), 0) / live.length) * 100) : 0;
  const options: ReadonlyArray<FilterOption<Status>> = (['live', 'scheduled', 'draft', 'expired', 'removed'] as const).map(s => ({
    key: s, label: STATUS_LABEL[s], count: count(s)
  }));

  const audienceText = (n: AdminNotification) =>
    n.audience === 'all'
      ? t('Mọi khách')
      : n.audience === 'type'
        ? n.customerTypes.map(id => audience.data?.types.find(x => x.id === id)?.name ?? t('Loại {n}', { n: id })).join(', ')
        : t('{n} khách chỉ định', { n: n.customerCodes.length });

  const doRemove = (n: AdminNotification) => {
    if (!window.confirm(t('Gỡ thông báo này khỏi portal của khách?'))) return;
    remove.mutate(n.id, { onSuccess: () => toast.show(t('Đã gỡ thông báo khỏi portal của khách'), 'success'), onError: e => toast.show(getErrorMessage(e), 'error') });
  };
  const doDelete = (n: AdminNotification) => {
    if (!window.confirm(t('Xóa hẳn thông báo này? Không khôi phục được.'))) return;
    del.mutate(n.id, {
      onSuccess: () => { setOpenId(null); toast.show(t('Đã xóa thông báo'), 'success'); },
      onError: e => toast.show(getErrorMessage(e), 'error')
    });
  };

  return (
    <div className={styles.wrap}>
      <div className={styles.head}>
        <div>
          <h2 className={styles.heading}>{t('Thông báo gửi khách')}</h2>
          <p className={styles.sub}>{t('Soạn và theo dõi tin gửi khách trên portal')}</p>
        </div>
        <Button variant="primary" onClick={() => setEditing('new')}>
          <Icon name="plus" size={15} /> {t('Soạn thông báo')}
        </Button>
      </div>

      <StatGrid>
        <StatCard label="Đang hiện" value={live.length} icon="bell" />
        <StatCard label="Tỉ lệ đọc trung bình" value={`${readRate}%`} icon="eye" tone="brand" />
        <StatCard label="Hẹn giờ" value={count('scheduled')} icon="clock" />
        <StatCard label="Khách có tài khoản" value={audience.data?.total ?? '—'} icon="user" />
      </StatGrid>

      <FilterBar options={options} value={filter} onChange={setFilter} query={query} onQuery={setQuery} />

      <div className={styles.card}>
        {list.isLoading && <p className={styles.note}>{t('Đang tải…')}</p>}
        {list.isError && <div className={styles.note}><Notice tone="danger">{getErrorMessage(list.error)}</Notice></div>}
        {!list.isLoading && !list.isError && !shown.length && <p className={styles.note}>{t('Chưa có thông báo nào.')}</p>}
        {shown.map(n => {
          const images = n.items.flatMap(i => i.images);
          const pct = n.recipients ? Math.min(100, Math.round((n.readCustomers / n.recipients) * 100)) : 0;
          return (
            <button key={n.id} type="button" className={cx(styles.row, n.important && styles.imp, openId === n.id && styles.active)} onClick={() => setOpenId(n.id)}>
              <span className={styles.text}>
                <span className={styles.title}>
                  <span className={styles.titleText}>{n.title}</span>
                  {n.pinned && <span className={cx(styles.chip, styles.chipPin)}>{t('Ghim')}</span>}
                  {n.important && <span className={cx(styles.chip, styles.chipImp)}>{t('Quan trọng')}</span>}
                  {n.items.length > 1 && <span className={styles.chip}>{t('{n} tin', { n: n.items.length })}</span>}
                </span>
                <span className={styles.meta}>
                  <span><Icon name="user" size={13} /> {audienceText(n)}</span>
                  {n.status !== 'draft' && n.status !== 'scheduled' && (
                    <span className={styles.progress}>
                      <span className={styles.bar}><i style={{ width: `${pct}%` }} /></span>
                      {t('{a}/{b} đã đọc', { a: n.readCustomers, b: n.recipients })}
                    </span>
                  )}
                  {n.emailStatus !== 'none' && <span className={cx(n.emailStatus === 'failed' && styles.bad)}><Icon name="mail" size={13} /> {t(EMAIL_LABEL[n.emailStatus])}</span>}
                  {images.length > 0 && <span><Icon name="image" size={13} /> {t('{n} ảnh', { n: images.length })}</span>}
                </span>
              </span>
              <span className={styles.side}>
                {images[0] && <img className={styles.thumb} src={notificationImageUrl(images[0])} alt="" loading="lazy" />}
                <span className={styles.time}>
                  {n.status === 'scheduled' ? t('Đăng {at}', { at: showTime(n.publishAt) }) : n.status === 'draft' ? n.createdAt : showTime(n.publishAt)}
                </span>
              </span>
            </button>
          );
        })}
      </div>

      <Drawer
        open={!!current}
        title={current ? `${t('Thông báo')} · ${t(STATUS_LABEL[current.status])}` : 'Thông báo'}
        onClose={() => setOpenId(null)}
        footer={
          current && (
            <>
              {(current.status === 'draft' || current.status === 'removed') && (
                <Button variant="ghost" className={styles.dangerBtn} onClick={() => doDelete(current)} disabled={del.isPending}>
                  <Icon name="trash" size={14} /> {t('Xóa hẳn')}
                </Button>
              )}
              {['live', 'scheduled', 'expired'].includes(current.status) && (
                <Button variant="ghost" className={styles.dangerBtn} onClick={() => doRemove(current)} disabled={remove.isPending}>
                  {t('Gỡ khỏi portal')}
                </Button>
              )}
              <Button onClick={() => setEditing({ copy: current })}>
                <Icon name="copy" size={14} /> {t('Nhân bản')}
              </Button>
              <Button variant="primary" onClick={() => setEditing(current)}>
                <Icon name="edit" size={14} /> {t(current.status === 'removed' ? 'Sửa và đăng lại' : 'Sửa')}
              </Button>
            </>
          )
        }
      >
        {current && <NotificationDetail notice={current} audienceText={audienceText(current)} />}
      </Drawer>

      {editing && (
        <NotificationEditor
          source={editing === 'new' || 'copy' in editing ? null : editing}
          copyOf={editing !== 'new' && 'copy' in editing ? editing.copy : null}
          onClose={() => setEditing(null)}
          onSaved={message => {
            setEditing(null);
            setOpenId(null);
            toast.show(t(message), 'success');
          }}
        />
      )}
    </div>
  );
}

function NotificationDetail({ notice: n, audienceText }: { notice: AdminNotification; audienceText: string }) {
  const { t } = useI18n();
  const readers = useNotificationReaders(n.status === 'draft' ? null : n.id);
  const pct = n.recipients ? Math.min(100, Math.round((n.readCustomers / n.recipients) * 100)) : 0;
  return (
    <>
      <NoticeContent
        title={n.title}
        meta={`${t('Việt An Express')} · ${showTime(n.publishAt)}`}
        imp={n.important}
        pin={n.pinned}
        parts={n.items}
      />

      <dl className={styles.facts}>
        <dt>{t('Gửi tới')}</dt>
        <dd>{audienceText}{n.audience === 'pick' && n.customerCodes.length > 0 && <small>{n.customerCodes.join(', ')}</small>}</dd>
        <dt>{t('Đăng')}</dt>
        <dd>{showTime(n.publishAt)}</dd>
        {n.expiresOn && (
          <>
            <dt>{t('Hiện đến')}</dt>
            <dd>{n.expiresOn.split('-').reverse().join('/')}</dd>
          </>
        )}
        <dt>{t('Email')}</dt>
        <dd>
          {t(EMAIL_LABEL[n.emailStatus])}
          {n.emailRecipients > 0 && ` · ${n.emailSent}/${n.emailRecipients}`}
          {n.emailError && <small className={styles.bad}>{n.emailError}</small>}
        </dd>
        <dt>{t('Người tạo')}</dt>
        <dd>{n.createdBy || '—'} · {n.createdAt}</dd>
      </dl>

      {n.status !== 'draft' && (
        <section className={styles.readers}>
          <header>
            <b>{t('{a} / {b} khách đã đọc', { a: n.readCustomers, b: n.recipients })}</b>
            <span className={styles.bar}><i style={{ width: `${pct}%` }} /></span>
          </header>
          {readers.isLoading && <p className={styles.note}>{t('Đang tải…')}</p>}
          {readers.data && !readers.data.length && <p className={styles.note}>{t('Chưa có khách nào đọc.')}</p>}
          <ul>
            {readers.data?.map(r => (
              <li key={r.customerCode}>
                <span>{r.customerCode}{r.companyName && ` · ${r.companyName}`}</span>
                <span>{r.accounts > 1 ? t('{n} tài khoản · {at}', { n: r.accounts, at: r.readAt }) : r.readAt}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}
