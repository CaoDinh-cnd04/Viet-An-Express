import { useState, type FormEvent } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, Icon, KeyValueList, Modal, Notice, StatusPill, Tabs, TextField } from '@/shared/ui';
import { Stars } from '@/features/feedback/components/Stars';
import { SurveyAnswers } from '@/features/feedback/components/SurveyAnswers';
import { averageRating } from '@/features/feedback/lib/images';
import type { AdminFeedbackItem } from '@/features/feedback/types';
import { adminLogout, getAdminToken, useAdminTroubles, useAdminFeedback, useAdminFeedbackDetail, useAdminImage, useAdminLogin, useDeleteFeedback, useMarkSeen } from '../api';
import { AdminNotifications } from '../components/AdminNotifications';
import { AdminTroubles } from '../components/AdminTroubles';
import { SurveyStats } from '../components/SurveyStats';
import styles from './AdminPage.module.css';

type Filter = 'all' | 'new';
type Section = 'feedback' | 'troubles' | 'notifications';

/**
 * Trang quản trị Việt An (/admin) — chỉ tài khoản quản trị cố định trong code đăng nhập được.
 * Tab Góp ý: xem góp ý khách gửi (nội dung, khảo sát, ảnh, người gửi), thống kê khảo sát, đánh dấu đã xem, xóa hẳn góp ý.
 * Tab Sự cố: sự cố khách báo, trả lời / đổi trạng thái (khách nhận thông báo riêng).
 * Tab Thông báo: soạn thông báo gửi mọi khách (tùy chọn gửi email), xem kết quả, xóa.
 */
export default function AdminPage() {
  const { t } = useI18n();
  const [signedIn, setSignedIn] = useState(() => !!getAdminToken());
  const [section, setSection] = useState<Section>('feedback');
  const feedback = useAdminFeedback(signedIn);
  const troubles = useAdminTroubles(signedIn);
  // Token hết hạn (401) → về màn đăng nhập.
  const expired = signedIn && feedback.isError && !getAdminToken();

  if (!signedIn || expired) return <AdminLogin onSignedIn={() => setSignedIn(true)} expired={expired} />;

  return (
    <main className={styles.page}>
      <div className={styles.wrap}>
        <header className={styles.head}>
          <div>
            <h1 className={styles.title}>{t('Quản trị Việt An Express')}</h1>
            <p className={styles.muted}>{t('Góp ý, sự cố của khách và thông báo gửi khách')}</p>
          </div>
          <Button size="sm" variant="ghost" onClick={() => { adminLogout(); setSignedIn(false); }}>
            <Icon name="arrowRight" size={14} /> {t('Đăng xuất')}
          </Button>
        </header>
        <Tabs
          ariaLabel="Chức năng quản trị"
          items={[
            { key: 'feedback', label: 'Góp ý', count: feedback.data?.filter(f => !f.seen).length || undefined },
            { key: 'troubles', label: 'Sự cố', count: troubles.data?.filter(x => x.ticket.status === 'new').length || undefined },
            { key: 'notifications', label: 'Thông báo' }
          ]}
          value={section}
          onChange={setSection}
        />
        {section === 'feedback' ? (
          <>
            <SurveyStats items={feedback.data ?? []} />
            <FeedbackList items={feedback.data ?? []} loading={feedback.isLoading} error={feedback.isError ? getErrorMessage(feedback.error) : null} />
          </>
        ) : section === 'troubles' ? (
          <AdminTroubles />
        ) : (
          <AdminNotifications />
        )}
      </div>
    </main>
  );
}

function AdminLogin({ onSignedIn, expired }: { onSignedIn: () => void; expired: boolean }) {
  const { t } = useI18n();
  const login = useAdminLogin();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const submit = (e: FormEvent) => {
    e.preventDefault();
    login.mutate({ userName, password }, { onSuccess: onSignedIn });
  };
  return (
    <main className={styles.page}>
      <form className={styles.login} onSubmit={submit}>
        <Card title="Quản trị Việt An Express">
          <div className={styles.loginBody}>
            {expired && <Notice tone="warning">{t('Phiên quản trị đã hết hạn, vui lòng đăng nhập lại.')}</Notice>}
            {login.isError && <Notice tone="danger">{getErrorMessage(login.error)}</Notice>}
            <TextField label="Tên đăng nhập" required autoComplete="username" value={userName} onChange={e => setUserName(e.target.value)} autoFocus />
            <TextField label="Mật khẩu" required type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} />
            <Button variant="primary" type="submit" disabled={login.isPending || !userName || !password}>
              {t(login.isPending ? 'Đang đăng nhập…' : 'Đăng nhập')}
            </Button>
          </div>
        </Card>
      </form>
    </main>
  );
}

function FeedbackList({ items, loading, error }: { items: AdminFeedbackItem[]; loading: boolean; error: string | null }) {
  const { t } = useI18n();
  const [filter, setFilter] = useState<Filter>('all');
  const [q, setQ] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);
  const seen = useMarkSeen();
  const unseen = items.filter(f => !f.seen).length;
  const avg = averageRating(items);
  const text = q.trim().toLowerCase();
  const shown = items.filter(f =>
    (filter === 'all' || !f.seen)
    && (!text || [f.message, f.companyName, f.customerCode, f.userName, f.contact ?? ''].some(v => v.toLowerCase().includes(text))));

  return (
    <Card
      title={t('Góp ý ({n})', { n: items.length })}
      subtitle={[unseen ? t('· {n} chưa xem', { n: unseen }) : null, avg ? t('· ★ {avg}/5 ({n} đánh giá)', { avg: avg.average, n: avg.count }) : null].filter(Boolean).join(' ') || undefined}
      actions={
        <div className={styles.tools}>
          <input className={styles.search} placeholder={t('Tìm nội dung, khách, người gửi…')} value={q} onChange={e => setQ(e.target.value)} />
          <Button size="sm" variant={filter === 'all' ? 'primary' : 'ghost'} onClick={() => setFilter('all')}>{t('Tất cả')}</Button>
          <Button size="sm" variant={filter === 'new' ? 'primary' : 'ghost'} onClick={() => setFilter('new')}>{t('Chưa xem')}</Button>
        </div>
      }
    >
      {error ? (
        <Notice tone="danger">{error}</Notice>
      ) : loading ? (
        <p className={styles.muted}>{t('Đang tải…')}</p>
      ) : !shown.length ? (
        <EmptyState title="Chưa có góp ý" description={items.length ? 'Không có góp ý khớp bộ lọc.' : 'Góp ý khách gửi sẽ hiện ở đây.'} />
      ) : (
        <ul className={styles.list}>
          {shown.map(f => (
            <li
              key={f.id}
              className={f.seen ? styles.item : `${styles.item} ${styles.unseen}`}
              role="button"
              tabIndex={0}
              title={t('Bấm để xem chi tiết')}
              onClick={() => setOpenId(f.id)}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  setOpenId(f.id);
                }
              }}
            >
              <div className={styles.itemHead}>
                <div>
                  <strong>{f.companyName || t('(không rõ công ty)')}</strong>
                  <span className={styles.muted}> · {f.customerCode} · {t(f.isStaff ? 'tài khoản con {name}' : 'tài khoản chính {name}', { name: f.userName })}</span>
                </div>
                <div className={styles.itemMeta}>
                  <Stars value={f.rating} />
                  <span className={styles.muted}>{f.createdAt}</span>
                  {f.seen ? (
                    <StatusPill tone="success">{t('Đã xem')}</StatusPill>
                  ) : (
                    <Button size="sm" disabled={seen.isPending} onClick={e => { e.stopPropagation(); seen.mutate(f.id); }}>
                      <Icon name="check" size={14} /> {t('Đánh dấu đã xem')}
                    </Button>
                  )}
                </div>
              </div>
              <p className={`${styles.message} ${styles.preview}`}>
                {f.message || <span className={styles.muted}>{t('Chỉ trả lời khảo sát ({n} câu)', { n: f.survey?.length ?? 0 })}</span>}
              </p>
              <div className={styles.itemFoot}>
                {f.images.length > 0 && <span className={styles.muted}><Icon name="image" size={14} /> {t('{n} ảnh', { n: f.images.length })}</span>}
                {f.contact && <span className={styles.muted}>{t('Liên hệ')}: {f.contact}</span>}
                <span className={styles.more}>{t('Xem chi tiết')} <Icon name="chevronRight" size={14} /></span>
              </div>
            </li>
          ))}
        </ul>
      )}
      <FeedbackDetail id={openId} onClose={() => setOpenId(null)} />
    </Card>
  );
}

/** Khung chi tiết 1 góp ý: khách + liên hệ của khách, người gửi, thời gian, nội dung đầy đủ, ảnh lớn. */
function FeedbackDetail({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t } = useI18n();
  const detail = useAdminFeedbackDetail(id);
  const remove = useDeleteFeedback();
  const d = detail.data;
  const f = d?.feedback;
  const close = () => {
    remove.reset();
    onClose();
  };
  const footer = f && (
    <Button
      variant="ghost"
      disabled={remove.isPending}
      onClick={() => {
        if (window.confirm(t('Xóa hẳn góp ý này cùng ảnh đính kèm? Khách cũng không còn thấy góp ý và không khôi phục được.'))) {
          remove.mutate(f.id, { onSuccess: close });
        }
      }}
    >
      <Icon name="trash" size={14} /> {t(remove.isPending ? 'Đang xóa…' : 'Xóa góp ý')}
    </Button>
  );
  return (
    <Modal open={!!id} title="Chi tiết góp ý" onClose={close} size="lg" footer={footer}>
      {remove.isError && <Notice tone="danger">{getErrorMessage(remove.error)}</Notice>}
      {detail.isError ? (
        <Notice tone="danger">{getErrorMessage(detail.error)}</Notice>
      ) : !d || !f ? (
        <p className={styles.muted}>{t('Đang tải…')}</p>
      ) : (
        <div className={styles.detail}>
          <KeyValueList
            items={[
              ['Khách hàng', `${f.companyName || t('(không rõ công ty)')} · ${f.customerCode}`],
              ['Người liên hệ của khách', d.customerContact || '—'],
              ['Điện thoại', d.customerPhone || '—'],
              ['Email', d.customerEmail || '—'],
              ['Địa chỉ', d.customerAddress || '—'],
              ['Người gửi góp ý', t(f.isStaff ? 'tài khoản con {name}' : 'tài khoản chính {name}', { name: f.userName })],
              ['Gửi lúc', f.createdAt],
              ['Đã xem lúc', d.seenAt || '—'],
              ['Liên hệ lại', f.contact || '—'],
              ['Đánh giá', f.rating ? <Stars value={f.rating} size="md" /> : t('Không chấm')]
            ]}
          />
          {!!f.survey?.length && (
            <div>
              <h3 className={styles.detailTitle}>{t('Khảo sát trải nghiệm')}</h3>
              <SurveyAnswers survey={f.survey} />
            </div>
          )}
          <div>
            <h3 className={styles.detailTitle}>{t('Nội dung góp ý')}</h3>
            <p className={styles.message}>{f.message || '—'}</p>
          </div>
          {f.images.length > 0 && (
            <div>
              <h3 className={styles.detailTitle}>{t('Ảnh đính kèm ({n}/{max})', { n: f.images.length, max: 5 })}</h3>
              <div className={styles.detailImages}>
                {f.images.map(img => <AdminImage key={img.id} id={img.id} name={img.fileName} large />)}
              </div>
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}

function AdminImage({ id, name, large }: { id: string; name: string; large?: boolean }) {
  const { t } = useI18n();
  const img = useAdminImage(id);
  if (!img.data) return <span className={styles.imagePlaceholder}>{img.isError ? t('Lỗi ảnh') : '…'}</span>;
  if (!large) {
    return (
      <a href={img.data} target="_blank" rel="noreferrer" title={name}>
        <img src={img.data} alt={name} />
      </a>
    );
  }
  return (
    <figure className={styles.largeImage}>
      <a href={img.data} target="_blank" rel="noreferrer" title={t('Mở ảnh cỡ thật')}>
        <img src={img.data} alt={name} />
      </a>
      <figcaption>
        <span title={name}>{name}</span>
        <a href={img.data} download={name}><Icon name="download" size={14} /> {t('Tải về')}</a>
      </figcaption>
    </figure>
  );
}
