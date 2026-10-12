import { useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Card, EmptyState, Icon, Notice, StatusPill, TextAreaField, TextField, type Tone } from '@/shared/ui';
import { NOTIFICATION_LINK_MAX, safeLink } from '@/features/notifications';
import { useAdminNotifications, useCreateNotification, useDeleteNotification, type AdminNotification } from '../api';
import styles from './AdminNotifications.module.css';

/** Giới hạn — khớp backend (Domain/Notification.cs). */
const LIMITS = { title: 200, body: 4000 } as const;

const EMAIL_STATUS: Record<AdminNotification['emailStatus'], { label: string; tone: Tone }> = {
  none: { label: 'Không gửi email', tone: 'neutral' },
  sending: { label: 'Đang gửi email…', tone: 'info' },
  sent: { label: 'Đã gửi email', tone: 'success' },
  partial: { label: 'Gửi email một phần', tone: 'warning' },
  failed: { label: 'Gửi email lỗi', tone: 'danger' },
  no_recipients: { label: 'Không có khách nào có email', tone: 'warning' }
};

/** Tab "Thông báo" của trang quản trị: soạn thông báo gửi mọi khách (hiện ở Trung tâm thông báo, tùy chọn gửi email), xem / xóa thông báo đã gửi. */
export function AdminNotifications() {
  const { t } = useI18n();
  const list = useAdminNotifications(true);
  const create = useCreateNotification();
  const remove = useDeleteNotification();
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [link, setLink] = useState('');
  const [important, setImportant] = useState(false);
  const [sendEmail, setSendEmail] = useState(false);
  const [done, setDone] = useState<string | null>(null);

  const linkError = link.trim() && !safeLink(link) ? 'Đường link phải bắt đầu bằng http:// hoặc https://' : undefined;
  const submit = () => {
    if (!title.trim() || !body.trim() || linkError) return;
    const confirmText = sendEmail
      ? t('Đăng thông báo cho mọi khách và gửi email tới khách có tài khoản portal (tối đa 450 địa chỉ)?')
      : t('Đăng thông báo cho mọi khách?');
    if (!window.confirm(confirmText)) return;
    setDone(null);
    create.mutate(
      { title: title.trim(), body: body.trim(), link: link.trim(), important, sendEmail },
      {
        onSuccess: message => {
          setDone(message);
          setTitle('');
          setBody('');
          setLink('');
          setImportant(false);
          setSendEmail(false);
        }
      }
    );
  };

  return (
    <>
      <Card title={t('Soạn thông báo')} subtitle={t('Khách thấy ở Trung tâm thông báo của portal')}>
        <div className={styles.form}>
          {done && <Notice tone="success">{t(done)}</Notice>}
          {create.isError && <Notice tone="danger">{getErrorMessage(create.error)}</Notice>}
          <TextField
            label="Tiêu đề"
            required
            maxLength={LIMITS.title}
            placeholder={t('VD: Lịch nghỉ lễ 2/9, phụ phí xăng dầu tháng 11…')}
            value={title}
            onChange={e => setTitle(e.target.value)}
          />
          <TextAreaField
            label="Nội dung"
            required
            rows={6}
            maxLength={LIMITS.body}
            aside={`${body.length}/${LIMITS.body}`}
            value={body}
            onChange={e => setBody(e.target.value)}
          />
          <TextField
            label="Đường link (không bắt buộc)"
            type="url"
            inputMode="url"
            maxLength={NOTIFICATION_LINK_MAX}
            placeholder="https://"
            hint={t('Khách thấy nút "Xem tại đây" dưới nội dung, bấm mở link ở tab mới')}
            error={linkError}
            value={link}
            onChange={e => setLink(e.target.value)}
          />
          <div className={styles.options}>
            <label className={styles.check}>
              <input type="checkbox" checked={important} onChange={e => setImportant(e.target.checked)} />
              <span>
                {t('Quan trọng')}
                <span className={styles.muted}> — {t('hiện popup khi khách mở portal')}</span>
              </span>
            </label>
            <label className={styles.check}>
              <input type="checkbox" checked={sendEmail} onChange={e => setSendEmail(e.target.checked)} />
              <span>
                {t('Gửi kèm email')}
                <span className={styles.muted}> — {t('tới email hồ sơ của khách có tài khoản portal và email nhân viên')}</span>
              </span>
            </label>
          </div>
          <div className={styles.actions}>
            <Button variant="primary" disabled={create.isPending || !title.trim() || !body.trim() || !!linkError} onClick={submit}>
              <Icon name="send" size={15} /> {t(create.isPending ? 'Đang đăng…' : 'Đăng thông báo')}
            </Button>
          </div>
        </div>
      </Card>

      <Card title={t('Thông báo đã gửi ({n})', { n: list.data?.length ?? 0 })} flush>
        {list.isError ? (
          <div className={styles.pad}><Notice tone="danger">{getErrorMessage(list.error)}</Notice></div>
        ) : list.isLoading ? (
          <p className={`${styles.pad} ${styles.muted}`}>{t('Đang tải…')}</p>
        ) : !list.data?.length ? (
          <EmptyState title="Chưa có thông báo" description="Thông báo đăng ở đây sẽ hiện cho mọi khách." />
        ) : (
          <ul className={styles.list}>
            {list.data.map(n => {
              const status = EMAIL_STATUS[n.emailStatus] ?? EMAIL_STATUS.none;
              return (
                <li key={n.id} className={styles.item}>
                  <div className={styles.itemHead}>
                    <strong className={styles.itemTitle}>{n.title}</strong>
                    {n.important && <StatusPill tone="warning">{t('Quan trọng')}</StatusPill>}
                    <Button
                      size="sm"
                      variant="ghost"
                      className={styles.delete}
                      disabled={remove.isPending}
                      aria-label={t('Xóa thông báo {title}', { title: n.title })}
                      onClick={() => {
                        if (window.confirm(t('Xóa thông báo này? Khách sẽ không còn thấy (email đã gửi không thu hồi được).'))) remove.mutate(n.id);
                      }}
                    >
                      <Icon name="trash" size={14} /> {t('Xóa')}
                    </Button>
                  </div>
                  <p className={styles.body}>{n.body}</p>
                  {safeLink(n.link) && (
                    <a className={styles.link} href={safeLink(n.link)!} target="_blank" rel="noopener noreferrer">
                      <Icon name="link" size={13} /> {n.link}
                    </a>
                  )}
                  <div className={styles.meta}>
                    <span>{n.createdAt}</span>
                    <span>{t('{n} tài khoản đã đọc', { n: n.readCount })}</span>
                    <StatusPill tone={status.tone}>{t(status.label)}</StatusPill>
                    {n.emailStatus !== 'none' && n.emailStatus !== 'sending' && n.emailRecipients > 0 && (
                      <span>{t('{sent}/{total} địa chỉ', { sent: n.emailSent, total: n.emailRecipients })}</span>
                    )}
                    {n.emailError && <span className={styles.error} title={n.emailError}>{n.emailError}</span>}
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </>
  );
}
