import { useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, Modal, Notice } from '@/shared/ui';
import { useTestNotificationEmail } from '../api';
import { toRequest, type Draft } from '../lib/editor';
import styles from './NotificationEditor.module.css';

const TEST_EMAIL_KEY = 'va.admin.testEmail';
const lastTestEmail = () => {
  try {
    return window.localStorage.getItem(TEST_EMAIL_KEY) ?? '';
  } catch {
    return '';
  }
};

/** Gửi thử email của nội dung đang soạn tới vài địa chỉ (nhớ địa chỉ lần trước trên máy này) — không lưu, không gửi khách. */
export function TestEmailDialog({ draft, onClose }: { draft: Draft; onClose: () => void }) {
  const { t } = useI18n();
  const send = useTestNotificationEmail();
  const [to, setTo] = useState(lastTestEmail);
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);

  const submit = () => {
    const { title, items, important } = toRequest(draft, true);
    setResult(null);
    send.mutate(
      { to, title, items, important },
      {
        onSuccess: r => {
          try {
            window.localStorage.setItem(TEST_EMAIL_KEY, to.trim());
          } catch {
            /* storage bị chặn: lần sau nhập lại */
          }
          setResult({ ok: true, text: t('Đã gửi email thử tới {n} địa chỉ — kiểm tra hộp thư (cả mục Spam).', { n: r.data }) });
        },
        onError: e => setResult({ ok: false, text: getErrorMessage(e) })
      }
    );
  };

  return (
    <Modal
      open
      title={t('Gửi thử email')}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>{t('Đóng')}</Button>
          <Button variant="primary" onClick={submit} disabled={!to.trim() || send.isPending}>
            <Icon name="send" size={15} /> {t(send.isPending ? 'Đang gửi…' : 'Gửi thử')}
          </Button>
        </>
      }
    >
      <div className={styles.testEmail}>
        <p className={styles.hint}>
          {t('Gửi nội dung đang soạn tới email của bạn để xem trước trên hộp thư. Thông báo không được lưu và không gửi cho khách.')}
        </p>
        <label className={styles.field}>
          {t('Email nhận thử')} <span>{t('· tối đa 5 địa chỉ, cách nhau dấu phẩy')}</span>
          <input
            className={styles.input}
            type="text"
            inputMode="email"
            value={to}
            onChange={e => setTo(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter' && to.trim() && !send.isPending) submit(); }}
            placeholder="ten@vietanexpress.com.vn"
            autoFocus
          />
        </label>
        {result && <Notice tone={result.ok ? 'success' : 'danger'}>{result.text}</Notice>}
      </div>
    </Modal>
  );
}
